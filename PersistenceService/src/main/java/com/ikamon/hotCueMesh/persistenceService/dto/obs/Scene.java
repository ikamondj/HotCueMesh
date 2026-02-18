package com.ikamon.hotCueMesh.persistenceService.dto.obs;

import java.util.List;

import lombok.Data;

@Data
public class Scene {
	private String name;
	private List<Source> sources;
}
