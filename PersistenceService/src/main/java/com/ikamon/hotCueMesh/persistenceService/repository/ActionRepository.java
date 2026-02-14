package com.ikamon.hotCueMesh.persistenceService.repository;

import org.springframework.data.jpa.repository.JpaRepository;
import com.ikamon.hotCueMesh.persistenceService.entity.Action;

public interface ActionRepository extends JpaRepository<Action, Long> {

}
