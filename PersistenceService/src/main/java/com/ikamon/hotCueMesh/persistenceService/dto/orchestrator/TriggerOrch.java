package com.ikamon.hotCueMesh.persistenceService.dto.orchestrator;

import java.util.Map;
import java.util.List;

import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
public class TriggerOrch {
	private Map<String, Boolean> hotCueType;
	private String cueMatchType;
	private Map<Integer, Boolean> cueColor;
	private Map<Integer, Boolean> decks;
	private String cueName;
	private List<TriggerActionOrch> actions;
}
