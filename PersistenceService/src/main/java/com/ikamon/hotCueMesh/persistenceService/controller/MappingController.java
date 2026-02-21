package com.ikamon.hotCueMesh.persistenceService.controller;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Map.Entry;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.bind.annotation.RequestParam;

import com.ikamon.hotCueMesh.persistenceService.dto.ActionTriggerRequest;
import com.ikamon.hotCueMesh.persistenceService.dto.TriggerDto;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.ActionRepository;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;
import com.ikamon.hotCueMesh.persistenceService.service.OrchestratorService;

import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;

import com.ikamon.hotCueMesh.persistenceService.dto.ActionDto;
import com.ikamon.hotCueMesh.persistenceService.dto.ConfigState;
import org.springframework.web.bind.annotation.GetMapping;




@Controller
public class MappingController {

    public MappingController(	@Autowired TriggerRepository triggerRepository,
				@Autowired ActionRepository actionRepository,
				@Autowired OrchestratorService orchestratorService) {
        this.triggerRepository = triggerRepository;
        this.actionRepository = actionRepository;
	this.orchestratorService = orchestratorService;
    }

    private final TriggerRepository triggerRepository;
    private final ActionRepository actionRepository;
    private final OrchestratorService orchestratorService;

    @PostMapping("configState")
    public ResponseEntity<String> postConfigState(@RequestBody ConfigState configState) {
        //TODO: process POST request
	triggerRepository.deleteAll();
	actionRepository.deleteAll();
	for (Entry<TriggerDto, List<ActionDto>> entry : configState.getConfig().entrySet()) {
		Trigger trigger = entry.getKey().toEntity(entry.getValue());
		triggerRepository.save(trigger);
	}

        return ResponseEntity.ok("Successfully updated mapping config!");
    }

    @GetMapping("getConfigState")
    public ResponseEntity<ConfigState> getConfigState() {
	ConfigState state = new ConfigState();
	List<Trigger> triggers = triggerRepository.findAllWithActions();
	Map<TriggerDto, List<ActionDto>> mapping = new HashMap<>();

	for (Trigger t : triggers) {
		ArrayList<ActionDto> actionDtos = new ArrayList<>();
		for (Action a : t.getActions()) {
			actionDtos.add(a.toDto());
		}
		mapping.put(t.toDto(), actionDtos);
	}
	state.setConfig(mapping);
        return ResponseEntity.ok(state);
    }


}
