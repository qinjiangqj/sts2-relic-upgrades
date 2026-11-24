extends SceneTree

const RESOURCE_ROOT = "res://aaacode_relic_forge/art"

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 4 or not args[3].is_valid_int():
		fail("Expected absolute art directory, scratch directory, output .pck, and relic count.")
		return
	var art_dir := args[0]
	var scratch := args[1]
	var output := args[2]
	var expected := args[3].to_int()
	var small_files := png_files(art_dir.path_join("small"))
	if small_files.size() != expected or small_files != png_files(art_dir.path_join("detail")):
		fail("The detail and small directories must contain the same %d PNG names." % expected)
		return
	var layout_path := art_dir.path_join("IconLayout.json")
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(layout_path))
	if not parsed is Dictionary or parsed.size() != expected:
		fail("IconLayout.json must describe every relic texture.")
		return
	var layout: Dictionary = parsed
	if DirAccess.make_dir_recursive_absolute(scratch) != OK:
		fail("Could not create the scratch directory.")
		return
	var packer := PCKPacker.new()
	if packer.pck_start(output) != OK:
		fail("Could not open the output PCK.")
		return
	for filename in small_files:
		var name := filename.get_basename()
		var icon := Image.load_from_file(art_dir.path_join("small").path_join(filename))
		var detail := Image.load_from_file(art_dir.path_join("detail").path_join(filename))
		var dimensions: Dictionary = layout.get(name, {})
		if not correct_image(icon, dimensions.get("small", [])) or not correct_image(detail, dimensions.get("detail", [])):
			fail("Invalid or opaque sprite: " + filename)
			return
		if not add_texture(packer, icon, scratch, "small", name) or \
				not add_texture(packer, detail, scratch, "detail", name) or \
				not add_texture(packer, make_outline(icon), scratch, "outline", name):
			fail("Could not add textures for " + filename)
			return
	if packer.flush() != OK:
		fail("Could not finalize the PCK.")
		return
	print("[RelicForgeArt] Packed %d relics and %d Godot textures." % [expected, expected * 3])
	quit()

func png_files(directory: String) -> PackedStringArray:
	var files := DirAccess.get_files_at(directory)
	var pngs := PackedStringArray()
	for file in files:
		if file.get_extension() == "png":
			pngs.append(file)
	return pngs

func correct_image(image: Image, size: Array) -> bool:
	return size.size() == 2 and image != null and not image.is_empty() and \
		image.get_size() == Vector2i(int(size[0]), int(size[1])) and \
		image.detect_alpha() != Image.ALPHA_NONE and image.get_used_rect().has_area()

func add_texture(packer: PCKPacker, image: Image, scratch: String, size: String, name: String) -> bool:
	var file := scratch.path_join("%s-%s.res" % [size, name])
	var texture := ImageTexture.create_from_image(image)
	if ResourceSaver.save(texture, file, ResourceSaver.FLAG_COMPRESS) != OK:
		return false
	return packer.add_file("%s/%s/%s.res" % [RESOURCE_ROOT, size, name], file) == OK

func make_outline(image: Image) -> Image:
	var size := image.get_size()
	var outline := Image.create_empty(size.x, size.y, false, Image.FORMAT_RGBA8)
	for y in size.y:
		for x in size.x:
			var original_alpha := image.get_pixel(x, y).a
			var nearby_alpha := 0.0
			for offset in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
				var point: Vector2i = Vector2i(x, y) + offset
				if Rect2i(Vector2i.ZERO, size).has_point(point):
					nearby_alpha = maxf(nearby_alpha, image.get_pixelv(point).a)
			outline.set_pixel(x, y, Color(1, 1, 1, maxf(0.0, nearby_alpha - original_alpha)))
	return outline

func fail(message: String) -> void:
	printerr("[RelicForgeArt] " + message)
	quit(1)
