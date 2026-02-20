package com.ikamon.hotCueMesh.persistenceService.constants;

import java.util.HashMap;
import java.util.Map;

public enum HotcueType {


	Hot_Cue(1),
	Saved_Loop(2),
	Action(4),
	Remix_Point(8),
	BeatGrid_Anchor(16),
	Automix_Point(32),
	Load_Point(64);
	private int val;
    	HotcueType(int x) {
		val = x;
	}
	public int getValue() {return val;}
	public static Map<String, Boolean> getHotcueTypeMap(int hcTypeValue) {
		Map<String, Boolean> result = new HashMap<>();
		for (HotcueType hcType : HotcueType.values()) {
			if ((hcType.getValue() & hcTypeValue) != 0) {
				result.put(hcType.name(), true);
			}
		}
		return result;
	}
}
