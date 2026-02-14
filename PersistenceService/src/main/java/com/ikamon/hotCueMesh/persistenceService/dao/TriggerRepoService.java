package com.ikamon.hotCueMesh.persistenceService.dao;

import org.springframework.beans.factory.annotation.Autowired;

import com.ikamon.hotCueMesh.persistenceService.dto.TriggerDto;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;
import com.ikamon.hotCueMesh.persistenceService.repository.TriggerRepository;

public class TriggerRepoService {
    @Autowired
    private TriggerRepository triggerRepository;

    public Trigger findTrigger(TriggerDto trigger) {
        return triggerRepository.findByCueNameAndCueColorAndHotcueTypeAndCueMatchType(trigger.getCueName(), trigger.getCueColor(), trigger.getHotcueType().name(), trigger.getCueMatchType().name());
    }

}