package com.ikamon.hotCueMesh.persistenceService.dto;

import java.util.List;
import java.util.Map;

import lombok.Data;

@Data
public class ConfigState {
	Map<TriggerDto, List<ActionDto>> config;
}
