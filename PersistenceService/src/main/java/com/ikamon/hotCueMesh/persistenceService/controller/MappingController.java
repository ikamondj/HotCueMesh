package com.ikamon.hotCueMesh.persistenceService.controller;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;
import org.springframework.web.bind.annotation.RequestParam;

import com.ikamon.hotCueMesh.persistenceService.dto.ActionTriggerRequest;
import com.ikamon.hotCueMesh.persistenceService.dto.TriggerDto;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.ActionRepository;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;

import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;



@Controller
public class MappingController {

    public MappingController(@Autowired TriggerRepository triggerRepository, @Autowired ActionRepository actionRepository) {
        this.triggerRepository = triggerRepository;
        this.actionRepository = actionRepository;
    }

    private final TriggerRepository triggerRepository;
    private final ActionRepository actionRepository;

    @PostMapping("addTrigger")
    public String addTrigger(@RequestBody TriggerDto trigger) {
        //TODO: process POST request
        Trigger trEntry = Trigger.builder()
                .cueName(trigger.getCueName())
                .cueColor(trigger.getCueColor())
                .hotcueType(trigger.getHotcueType())
                .cueMatchType(trigger.getCueMatchType())
                .enabled(true)
                .build();
        triggerRepository.save(trEntry);
        return "Trigger added";
    }

    @PostMapping("addActionToTrigger")
    public String addActionToTrigger(@RequestBody ActionTriggerRequest req) {

        return "Action added to trigger";
    }

    @PostMapping("removeActionFromTrigger")
    public String removeActionFromTrigger(@RequestBody ActionTriggerRequest req) {
        //TODO: process POST request
        
        return "Action removed from trigger";
    }

    @PostMapping("removeTrigger")
    public String removeTrigger(@RequestBody Trigger trigger) {

        return "Trigger removed";
    }
    
    
}
