package com.ikamon.hotCueMesh.persistenceService.dto;

import java.util.List;

import com.ikamon.hotCueMesh.persistenceService.dto.obs.Scene;

import lombok.Data;


@Data
public class ObsState {
	private List<Scene> scenes;
}
