project(hot-cue-mesh)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTOUIC ON)
set(CMAKE_AUTORCC ON)


set(hot_cue_mesh_SOURCES
    OBSReceiverPlugin/src/Plugin.cpp
)

add_library(hot-cue-mesh MODULE ${hot_cue_mesh_SOURCES})

target_include_directories(hot-cue-mesh PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Link against OBS APIs
target_link_libraries(hot-cue-mesh
    PRIVATE
        libobs
)

# Ensure plugin loads correctly on each platform
if(OS_WINDOWS)
    set_target_properties(hot-cue-mesh PROPERTIES
        PREFIX ""
        SUFFIX ".dll"
    )
elseif(OS_MACOS)
    set_target_properties(hot-cue-mesh PROPERTIES
        BUNDLE TRUE
        BUNDLE_EXTENSION ".plugin"
    )
else()
    set_target_properties(hot-cue-mesh PROPERTIES
        PREFIX ""
        SUFFIX ".so"
    )
endif()
