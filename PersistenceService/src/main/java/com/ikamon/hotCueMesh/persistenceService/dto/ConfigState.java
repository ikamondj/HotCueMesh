package com.ikamon.hotCueMesh.persistenceService.dto;

import java.util.Map;

import lombok.Data;

@Data
public class ConfigState {
	Map<TriggerDto, ActionDto> config;
}
