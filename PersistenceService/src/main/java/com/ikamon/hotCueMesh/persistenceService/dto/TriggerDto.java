package com.ikamon.hotCueMesh.persistenceService.dto;
import java.util.List;

import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import com.ikamon.hotCueMesh.persistenceService.constants.Decks;
import com.ikamon.hotCueMesh.persistenceService.constants.HotcueType;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;

import lombok.Builder;
import lombok.Data;
import lombok.Setter;
import lombok.Getter;

@Getter
@Setter
@Builder
public class TriggerDto {
    List<String> hotcueType;
    int cueColor;
    String cueName;
    Boolean enabled;
    List<Integer> decks;
    CueMatch cueMatchType;
    public int getHotcueIntEncoding() {
	int result = 0;
	for (String hcType : hotcueType) {
		result |= HotcueType.valueOf(hcType).getValue();
	}
	return result;
    }

    public int getDeckIntEncoding() {
	int result = 0;
	for (int x : decks) {
		for (int y : Decks.decks) {
			if (x == y) {
				result |= y;
			}
		}
	}
	return result;
    }
}
