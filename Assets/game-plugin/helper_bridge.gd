extends Node

const PLUGIN_VERSION := "1.0.2"
const PROTOCOL := 3
const FIRST_PORT := 19110
const LAST_PORT := 19120
const MIN_ACTION_GAP_MS := 2000
const MAX_REQUEST_BYTES := 1048576
const EVENT_LIMIT := 128

var _server := TCPServer.new()
var _port := 0
var _kernel: Node
var _clients: Array[Dictionary] = []
var _pending_commands: Dictionary = {}
var _crate_events: Array[Dictionary] = []
var _event_sequence := 0
var _last_action_ms := -MIN_ACTION_GAP_MS
var _last_status_write_ms := -1000
var _icon_cache: Dictionary = {}


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_bind_kernel()
	_start_listener()
	_write_status()


func _process(_delta: float) -> void:
	if _kernel == null or not is_instance_valid(_kernel):
		_bind_kernel()
	_accept_clients()
	_poll_clients()
	_expire_pending_commands()
	var now := Time.get_ticks_msec()
	if now - _last_status_write_ms >= 1000:
		_write_status()


func _exit_tree() -> void:
	if _server.is_listening():
		_server.stop()
	for entry in _clients:
		var peer: StreamPeerTCP = entry.get("peer")
		if peer != null:
			peer.disconnect_from_host()
	_clients.clear()
	_pending_commands.clear()


func _bind_kernel() -> void:
	_kernel = get_node_or_null("/root/AppKernel")
	if _kernel == null:
		return
	if _kernel.has_signal("ui_snapshot_changed"):
		var callback := Callable(self, "_on_kernel_snapshot_changed")
		if not _kernel.is_connected("ui_snapshot_changed", callback):
			_kernel.connect("ui_snapshot_changed", callback)
	print("Desktop Racer Helper API v%s bound to AppKernel" % PLUGIN_VERSION)


func _start_listener() -> void:
	if _server.is_listening():
		return
	for candidate in range(FIRST_PORT, LAST_PORT + 1):
		var error := _server.listen(candidate, "127.0.0.1")
		if error == OK:
			_port = candidate
			return
	push_error("Desktop Racer Helper API could not bind a loopback port")


func _accept_clients() -> void:
	if not _server.is_listening():
		_start_listener()
		return
	for _attempt in range(8):
		if not _server.is_connection_available():
			break
		var peer := _server.take_connection()
		if peer == null:
			continue
		peer.set_no_delay(true)
		_clients.append({
			"peer": peer,
			"buffer": PackedByteArray(),
			"pending": false,
			"request_id": "",
		})


func _poll_clients() -> void:
	for index in range(_clients.size() - 1, -1, -1):
		var entry: Dictionary = _clients[index]
		var peer: StreamPeerTCP = entry.get("peer")
		if peer == null:
			_clients.remove_at(index)
			continue
		peer.poll()
		if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
			_remove_client(index)
			continue
		if bool(entry.get("pending", false)):
			continue
		var available := peer.get_available_bytes()
		if available <= 0:
			continue
		var received := peer.get_data(available)
		if int(received[0]) != OK:
			_remove_client(index)
			continue
		var buffer: PackedByteArray = entry.get("buffer", PackedByteArray())
		buffer.append_array(received[1])
		if buffer.size() > MAX_REQUEST_BYTES:
			_send_error(peer, "", "request_too_large")
			_remove_client(index)
			continue
		var newline := buffer.find(10)
		if newline < 0:
			entry["buffer"] = buffer
			_clients[index] = entry
			continue
		var line := buffer.slice(0, newline).get_string_from_utf8().strip_edges()
		var request: Variant = JSON.parse_string(line)
		if request is not Dictionary:
			_send_error(peer, "", "invalid_json")
			_remove_client(index)
			continue
		entry["buffer"] = PackedByteArray()
		_clients[index] = entry
		_handle_request(index, request as Dictionary)


func _handle_request(client_index: int, request: Dictionary) -> void:
	var peer: StreamPeerTCP = _clients[client_index].get("peer")
	var request_id := str(request.get("id", ""))
	var operation := str(request.get("op", ""))
	match operation:
		"ping":
			_send_ok(peer, request_id, _runtime_view())
		"snapshot":
			if _kernel == null:
				_send_error(peer, request_id, "kernel_not_ready")
			else:
				_send_ok(peer, request_id, {"snapshot": _snapshot_with_icons()})
		"events":
			_send_ok(peer, request_id, {"events": _events_after(int(request.get("after", 0)))})
		"action":
			_handle_action(client_index, request)
		_:
			_send_error(peer, request_id, "unknown_operation")


func _handle_action(client_index: int, request: Dictionary) -> void:
	var peer: StreamPeerTCP = _clients[client_index].get("peer")
	var request_id := str(request.get("id", ""))
	if _kernel == null or not _is_online():
		_send_error(peer, request_id, "game_not_online")
		return
	var action := str(request.get("action", ""))
	if action not in ["open_next_crate", "contribute_collection_part", "dismantle_item", "sell_item", "repair_car"]:
		_send_error(peer, request_id, "action_not_allowed")
		return
	if not _valid_action_payload(action, request.get("payload", {})):
		_send_error(peer, request_id, "invalid_payload")
		return
	var now := Time.get_ticks_msec()
	if now - _last_action_ms < MIN_ACTION_GAP_MS:
		_send_error(peer, request_id, "action_rate_limited")
		return
	if not _pending_commands.is_empty():
		_send_error(peer, request_id, "game_command_pending")
		return
	var result: Variant = _kernel.call("handle_ui_action", StringName(action), request.get("payload", {}))
	if result is not String or str(result).is_empty():
		_send_error(peer, request_id, "game_rejected_command")
		return
	_last_action_ms = now
	var command_id := str(result)
	_clients[client_index]["pending"] = true
	_clients[client_index]["request_id"] = request_id
	_pending_commands[command_id] = {
		"peer": peer,
		"request_id": request_id,
		"action": action,
		"submitted_ms": now,
	}


func _valid_action_payload(action: String, payload_value: Variant) -> bool:
	if payload_value is not Dictionary:
		return false
	var payload: Dictionary = payload_value
	match action:
		"open_next_crate":
			return payload.is_empty()
		"contribute_collection_part":
			return (
				not str(payload.get("item_id", "")).is_empty()
				and not str(payload.get("set_id", "")).is_empty()
				and not str(payload.get("archetype_id", "")).is_empty()
			)
		"dismantle_item", "sell_item":
			var item_ids: Variant = payload.get("item_ids", [])
			if item_ids is not Array or (item_ids as Array).is_empty() or (item_ids as Array).size() > 100:
				return false
			for item_id in item_ids:
				if str(item_id).is_empty():
					return false
			return true
		"repair_car":
			return typeof(payload.get("car_id", "")) == TYPE_STRING
	return false


func _on_kernel_snapshot_changed(delta: Dictionary) -> void:
	var completed_value: Variant = delta.get("command_completed", null)
	if completed_value is not Dictionary:
		return
	var completed: Dictionary = completed_value
	var command_id := str(completed.get("command_id", ""))
	var action := str(completed.get("action", ""))
	if action == "open_next_crate" and bool(completed.get("ok", false)):
		_record_crate_event(delta)
	if not _pending_commands.has(command_id):
		return
	var pending: Dictionary = _pending_commands[command_id]
	_pending_commands.erase(command_id)
	var peer: StreamPeerTCP = pending.get("peer")
	if peer == null or peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	_send_ok(peer, str(pending.get("request_id", "")), {
		"completed": _json_safe(completed),
		"item_obtained": _json_safe(delta.get("item_obtained", {})),
		"snapshot": _snapshot_with_icons(),
	})
	for index in range(_clients.size() - 1, -1, -1):
		if _clients[index].get("peer") == peer:
			_clients[index]["pending"] = false
			_clients[index]["request_id"] = ""
			break


func _record_crate_event(delta: Dictionary) -> void:
	_event_sequence += 1
	var event_id := "%d:%d" % [OS.get_process_id(), _event_sequence]
	var item: Variant = delta.get("item_obtained", {})
	_crate_events.append({
		"sequence": _event_sequence,
		"event_id": event_id,
		"opened_at_unix_ms": int(Time.get_unix_time_from_system() * 1000.0),
		"item": _json_safe(item),
	})
	while _crate_events.size() > EVENT_LIMIT:
		_crate_events.pop_front()


func _events_after(sequence: int) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for event in _crate_events:
		if int(event.get("sequence", 0)) > sequence:
			result.append(event.duplicate(true))
	return result


func _snapshot_with_icons() -> Dictionary:
	if _kernel == null or not _kernel.has_method("get_ui_snapshot"):
		return {}
	var snapshot_value: Variant = _kernel.call("get_ui_snapshot")
	if snapshot_value is not Dictionary:
		return {}
	var snapshot: Dictionary = (snapshot_value as Dictionary).duplicate(true)
	var inventory_value: Variant = snapshot.get("inventory", {})
	if inventory_value is Dictionary:
		var inventory: Dictionary = inventory_value
		var items_value: Variant = inventory.get("items", [])
		if items_value is Array:
			for item_value in items_value:
				if item_value is Dictionary:
					var item: Dictionary = item_value
					var icon := _item_icon_base64(item)
					if not icon.is_empty():
						item["helper_icon_png"] = icon
		return _json_safe(snapshot)
	return _json_safe(snapshot)


func _item_icon_base64(item: Dictionary) -> String:
	var slot := str(item.get("slot_id", ""))
	var rarity := str(item.get("rarity_id", "common"))
	var sprite := str(item.get("sprite_path", ""))
	var cache_key := "%s|%s|%s" % [slot, rarity, sprite]
	if _icon_cache.has(cache_key):
		return str(_icon_cache[cache_key])
	var texture: Texture2D
	if not sprite.is_empty():
		texture = load(sprite) as Texture2D
	if texture == null and not slot.is_empty():
		texture = PartRarityIconAtlas.centered_prominent_item_icon(slot, rarity)
	if texture == null:
		_icon_cache[cache_key] = ""
		return ""
	var image := texture.get_image()
	if image == null or image.is_empty():
		_icon_cache[cache_key] = ""
		return ""
	var encoded := Marshalls.raw_to_base64(image.save_png_to_buffer())
	_icon_cache[cache_key] = encoded
	return encoded


func _is_online() -> bool:
	if _kernel == null:
		return false
	var authority: Variant = _kernel.get("session_authority")
	if authority == null or not is_instance_valid(authority):
		return false
	return (
		bool(authority.call("is_authenticated"))
		and str(authority.get("connection_state")) == "online"
	)


func _runtime_view() -> Dictionary:
	var state := "not_ready"
	var authenticated := false
	if _kernel != null:
		var authority: Variant = _kernel.get("session_authority")
		if authority != null and is_instance_valid(authority):
			state = str(authority.get("connection_state"))
			authenticated = bool(authority.call("is_authenticated"))
	return {
		"plugin_version": PLUGIN_VERSION,
		"protocol": PROTOCOL,
		"host_pid": OS.get_process_id(),
		"port": _port,
		"engine_version": str(Engine.get_version_info().get("string", "")),
		"game_version": str(ProjectSettings.get_setting("application/config/version", "")),
		"kernel_ready": _kernel != null,
		"authenticated": authenticated,
		"connection_state": state,
		"connected": _is_online(),
		"heartbeat_unix_ms": int(Time.get_unix_time_from_system() * 1000.0),
	}


func _write_status() -> void:
	_last_status_write_ms = Time.get_ticks_msec()
	var appdata := OS.get_environment("LOCALAPPDATA")
	if appdata.is_empty():
		return
	var directory := appdata.path_join("DesktopRacerHelper")
	if DirAccess.make_dir_recursive_absolute(directory) != OK and not DirAccess.dir_exists_absolute(directory):
		return
	var path := directory.path_join("game-api-status.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return
	file.store_string(JSON.stringify(_runtime_view()))
	file.close()


func _send_ok(peer: StreamPeerTCP, request_id: String, data: Variant) -> void:
	_send(peer, {"id": request_id, "ok": true, "data": _json_safe(data)})


func _send_error(peer: StreamPeerTCP, request_id: String, error_code: String) -> void:
	_send(peer, {"id": request_id, "ok": false, "error": error_code})


func _send(peer: StreamPeerTCP, value: Dictionary) -> void:
	if peer == null or peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		return
	peer.put_data((JSON.stringify(_json_safe(value)) + "\n").to_utf8_buffer())


func _remove_client(index: int) -> void:
	var entry: Dictionary = _clients[index]
	var peer: StreamPeerTCP = entry.get("peer")
	if peer != null:
		for command_id in _pending_commands.keys():
			if _pending_commands[command_id].get("peer") == peer:
				_pending_commands.erase(command_id)
		peer.disconnect_from_host()
	_clients.remove_at(index)


func _expire_pending_commands() -> void:
	var now := Time.get_ticks_msec()
	for command_id in _pending_commands.keys():
		var pending: Dictionary = _pending_commands[command_id]
		if now - int(pending.get("submitted_ms", now)) < 120000:
			continue
		_pending_commands.erase(command_id)
		_send_error(pending.get("peer"), str(pending.get("request_id", "")), "result_not_confirmed")


func _json_safe(value: Variant) -> Variant:
	match typeof(value):
		TYPE_NIL, TYPE_BOOL, TYPE_INT, TYPE_STRING:
			return value
		TYPE_FLOAT:
			return value if is_finite(value) else 0.0
		TYPE_STRING_NAME:
			return str(value)
		TYPE_DICTIONARY:
			var result: Dictionary = {}
			for key in value:
				result[str(key)] = _json_safe(value[key])
			return result
		TYPE_ARRAY:
			var result: Array = []
			for child in value:
				result.append(_json_safe(child))
			return result
		TYPE_PACKED_BYTE_ARRAY:
			return Marshalls.raw_to_base64(value)
		TYPE_PACKED_INT32_ARRAY, TYPE_PACKED_INT64_ARRAY, TYPE_PACKED_FLOAT32_ARRAY, TYPE_PACKED_FLOAT64_ARRAY, TYPE_PACKED_STRING_ARRAY, TYPE_PACKED_VECTOR2_ARRAY, TYPE_PACKED_VECTOR3_ARRAY, TYPE_PACKED_COLOR_ARRAY:
			var result: Array = []
			for child in value:
				result.append(_json_safe(child))
			return result
		TYPE_VECTOR2:
			return [value.x, value.y]
		TYPE_VECTOR2I:
			return [value.x, value.y]
		TYPE_VECTOR3:
			return [value.x, value.y, value.z]
		TYPE_VECTOR3I:
			return [value.x, value.y, value.z]
		TYPE_COLOR:
			return [value.r, value.g, value.b, value.a]
		TYPE_OBJECT:
			return null
		_:
			return str(value)
