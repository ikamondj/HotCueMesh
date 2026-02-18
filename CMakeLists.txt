project(HotCueMesh)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTOUIC ON)
set(CMAKE_AUTORCC ON)

include(FetchContent)


FetchContent_Declare(
  httplib
  GIT_REPOSITORY https://github.com/yhirose/cpp-httplib.git
  GIT_TAG v0.15.3
)
FetchContent_MakeAvailable(httplib)

FetchContent_Declare(
  nlohmann_json
  GIT_REPOSITORY https://github.com/nlohmann/json.git
  GIT_TAG v3.11.3
)
FetchContent_MakeAvailable(nlohmann_json)


set(hot_cue_mesh_SOURCES
    OBSReceiverPlugin/Plugin.cpp
    OBSReceiverPlugin/Channel.cpp
    OBSReceiverPlugin/ObsEvents.cpp
    OBSReceiverPlugin/StateReader.cpp
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
        httplib::httplib
        nlohmann_json::nlohmann_json
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
