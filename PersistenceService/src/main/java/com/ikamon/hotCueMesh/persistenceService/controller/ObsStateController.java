package com.ikamon.hotCueMesh.persistenceService.controller;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestParam;

import com.ikamon.hotCueMesh.persistenceService.dto.ObsState;
import com.ikamon.hotCueMesh.persistenceService.service.ObsStateService;


@Controller
public class ObsStateController {
	private final ObsStateService obsStateService;
	public ObsStateController(@Autowired ObsStateService obsStateService) {
		this.obsStateService = obsStateService;
	}
	@GetMapping("obsState")
	public ResponseEntity<ObsState> getMethodName() {
		try {
			ObsState obsState = obsStateService.getObsState();
			return ResponseEntity.ok(obsState);
		} catch (IllegalStateException ex) {
			return ResponseEntity.status(503).build();
		}
	}

}
