package com.ikamon.hotCueMesh.persistenceService.entity;

import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.FetchType;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import lombok.Getter;
import lombok.Setter;


@Getter
@Setter
@Entity(name="ACTION")
public class Action {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private long actionId;
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "triggerId", nullable = false)
    private Trigger trigger;
    @Column(nullable = false)
    private String appId;
    @Column(nullable = false)
    private String actionType;
    @Column(nullable = false)
    private String actionArgs;
}
