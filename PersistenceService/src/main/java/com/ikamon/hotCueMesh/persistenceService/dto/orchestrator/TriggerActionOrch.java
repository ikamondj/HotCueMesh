package com.ikamon.hotCueMesh.persistenceService.dto.orchestrator;

import java.util.Map;

import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@NoArgsConstructor
public class TriggerActionOrch {
	private String appId;
	private String actionType;
	private Map<String, String> args;
}
