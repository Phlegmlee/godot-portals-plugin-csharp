shader_type spatial;

#include "res://addons/portalsCS/materials/portalclip_mesh.gdshaderinc"

PORTALCLIP_UNIFORMS

void vertex() {
	PORTALCLIP_VERTEX
}

void fragment() {
	PORTALCLIP_FRAGMENT
}