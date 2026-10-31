@tool
extends EditorScenePostImportPlugin

## glTF 2.0 says COLOR_0 multiplies the base colour. Godot 4.7's GLTFDocument::_parse_meshes only sets
## vertex_color_use_as_albedo once an *earlier* primitive of the same mesh had COLOR_0, so the first
## primitive of every mesh (the stone, the vines) loses the kit's baked tint + cavity AO.
## Upstream: godotengine/godot#120625 (regression since 4.7 dev5), fixed by #122315 for 4.8, not
## cherry-picked to 4.7.x. On 4.8+ this plugin is redundant but harmless. (The "-vcol" name-suffix
## workaround also forces vertex_color_is_srgb, which darkens the kit's linear COLOR_0.)
## Every kit mesh is exported with COLOR_0, so the flag is re-applied to the kit materials by name.
## (Patching the ImporterMesh surfaces at MESH_3D_NODE is not enough: the importer re-applies the
## material sub-resources afterwards.)

const KIT_MATERIAL_PREFIXES: PackedStringArray = ["M_Temple_", "M_Dry_Vines"]


func _internal_process(category: int, _base_node: Node, _node: Node, resource: Resource) -> void:
	if category != INTERNAL_IMPORT_CATEGORY_MATERIAL:
		return
	var mat := resource as BaseMaterial3D
	if mat != null and _is_kit_material(mat.resource_name):
		mat.vertex_color_use_as_albedo = true


func _is_kit_material(material_name: String) -> bool:
	for prefix in KIT_MATERIAL_PREFIXES:
		if material_name.begins_with(prefix):
			return true
	return false
