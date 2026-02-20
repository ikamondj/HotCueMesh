package com.ikamon.hotCueMesh.persistenceService.controller;

import java.util.Map.Entry;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.bind.annotation.RequestParam;

import com.ikamon.hotCueMesh.persistenceService.dto.ActionTriggerRequest;
import com.ikamon.hotCueMesh.persistenceService.dto.TriggerDto;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.ActionRepository;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;
import com.ikamon.hotCueMesh.persistenceService.service.OrchestratorService;

import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;

import com.ikamon.hotCueMesh.persistenceService.dto.ActionDto;
import com.ikamon.hotCueMesh.persistenceService.dto.ConfigState;



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

    @PostMapping("addTrigger")
    public String addTrigger(@RequestBody TriggerDto trigger) {
        //TODO: process POST request
        Trigger trEntry = Trigger.builder()
                .cueName(trigger.getCueName())
                .cueColor(trigger.getCueColor())
                .hotcueType(trigger.getHotcueIntEncoding())
                .cueMatchType(trigger.getCueMatchType())
                .enabled(true)
                .build();
        triggerRepository.save(trEntry);
        orchestratorService.update();
        return "Trigger added";
    }

    @PostMapping("addActionToTrigger")
    public String addActionToTrigger(@RequestBody ActionTriggerRequest req) {
	//actionRepository.save(entity);
	orchestratorService.update();
        return "Action added to trigger";
    }

    @PostMapping("removeActionFromTrigger")
    public String removeActionFromTrigger(@RequestBody ActionTriggerRequest req) {
        //TODO: process POST request

	orchestratorService.update();
        return "Action removed from trigger";
    }

    @PostMapping("removeTrigger")
    public String removeTrigger(@RequestBody TriggerDto trigger) {
	Trigger trig = triggerRepository.findByCueNameAndCueColorAndHotcueTypeAndCueMatchType(
		trigger.getCueName(),
		trigger.getCueColor(),
		trigger.getHotcueIntEncoding(),
		trigger.getCueMatchType().name());
	triggerRepository.delete(trig);
	orchestratorService.update();
        return "Trigger removed";
    }

    @PostMapping("configState")
    public ResponseEntity<String> postMethodName(@RequestBody ConfigState configState) {
        //TODO: process POST request
	triggerRepository.deleteAll();
	actionRepository.deleteAll();
	for (Entry<TriggerDto, ActionDto> entry : configState.getConfig().entrySet()) {
		Trigger trigger = entry.getKey().toEntity();
		triggerRepository.save(trigger);
	}

        return ResponseEntity.ok("Successfully updated mapping config!");
    }



}
