package com.ikamon.hotCueMesh.persistenceService.dto;
import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import com.ikamon.hotCueMesh.persistenceService.constants.HotcueType;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;
import com.ikamon.hotCueMesh.persistenceService.entity.Trigger;

import lombok.Builder;
import lombok.Data;
import lombok.Setter;
import lombok.Getter;

@Getter
@Setter
@Builder
public class TriggerDto {
    HotcueType hotcueType;
    int cueColor;
    String cueName;
    Boolean enabled;
    CueMatch cueMatchType;
}