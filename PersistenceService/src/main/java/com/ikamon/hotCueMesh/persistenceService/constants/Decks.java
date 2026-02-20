package com.ikamon.hotCueMesh.persistenceService.constants;

import java.util.HashMap;
import java.util.Map;

public class Decks {
	public static int[] decks = {1,2,4,8};
	public static Map<Integer, Boolean> getDeckMap(int deck) {
		Map<Integer, Boolean> result = new HashMap<>();
		for (int dec : decks) {
			if ((deck & dec) != 0) {
				result.put(dec, true);
			}
		}
		return result;
	}
}
