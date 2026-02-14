package com.ikamon.hotCueMesh.persistenceService.entity;

import com.ikamon.hotCueMesh.persistenceService.constants.HotcueType;
import com.ikamon.hotCueMesh.persistenceService.constants.CueMatch;
import jakarta.persistence.*;
import lombok.Builder;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

@Getter
@Setter
@Entity
@Builder
@Table(name = "TRIGGER")
public class Trigger {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private long triggerId;

    @Enumerated(EnumType.STRING)
    @Column(nullable = true)
    private HotcueType hotcueType;

    @Column(nullable = false)
    private int cueColor; // <- was int

    @Column(nullable = true)
    private String cueName;

    @Column(nullable = false)
    private Boolean enabled;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private CueMatch cueMatchType;

    @OneToMany(mappedBy = "trigger")
    private List<Action> actions;
}
