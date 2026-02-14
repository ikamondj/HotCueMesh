package com.ikamon.hotCueMesh.persistenceService.dto;

import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import com.ikamon.hotCueMesh.persistenceService.constants.HotcueType;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.dto.TriggerDto;
import lombok.Data;

public record ActionTriggerRequest (
        TriggerDto trigger,
        Action action
) {}

