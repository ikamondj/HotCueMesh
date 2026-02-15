project(HotCueMesh)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTOUIC ON)
set(CMAKE_AUTORCC ON)


set(hot_cue_mesh_SOURCES
    OBSReceiverPlugin/Plugin.cpp
    OBSReceiverPlugin/Channel.cpp
    OBSReceiverPlugin/ObsEvents.cpp
)

add_library(HotCueMesh MODULE ${hot_cue_mesh_SOURCES})

target_include_directories(HotCueMesh PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/OBSReceiverPlugin
)

# Link against OBS APIs
target_link_libraries(HotCueMesh
    PRIVATE
        libobs
)

# Ensure plugin loads correctly on each platform
if(OS_WINDOWS)
    set_target_properties(HotCueMesh PROPERTIES
        PREFIX ""
        SUFFIX ".dll"
    )
elseif(OS_MACOS)
    set_target_properties(HotCueMesh PROPERTIES
        BUNDLE TRUE
        BUNDLE_EXTENSION ".plugin"
    )
else()
    set_target_properties(HotCueMesh PROPERTIES
        PREFIX ""
        SUFFIX ".so"
    )
endif()
