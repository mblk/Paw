cmake_minimum_required(VERSION 3.26)

if(NOT DEFINED CMAKE_CXX_COMPILER)
    set(CMAKE_CXX_COMPILER clang++)
endif()

project(Paw.Profiler.Linux LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(PAW_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../..")
set(PAW_PROFILER_SHARED_DIR "${PAW_ROOT}/src/Paw.Profiler.Shared")
set(CORECLR_REPO "" CACHE PATH "Path to the dotnet/runtime repo")

if(NOT EXISTS "${CORECLR_REPO}/src/coreclr/pal/inc")
    message(FATAL_ERROR "CORECLR_REPO must point to the dotnet/runtime repository root.")
endif()

add_library(PawProfilerLinux SHARED

    "${PAW_PROFILER_SHARED_DIR}/CoreProfiler.cc"
    "${PAW_PROFILER_SHARED_DIR}/CoreProfilerFactory.cc"
    "${PAW_PROFILER_SHARED_DIR}/DllMain.cc"
    "${PAW_PROFILER_SHARED_DIR}/Logger.cc"
    "${PAW_PROFILER_SHARED_DIR}/OS.cc"

    "${CORECLR_REPO}/src/coreclr/pal/prebuilt/idl/corprof_i.cpp"
    "${CORECLR_REPO}/src/native/minipal/guid.c"
)

set_target_properties(PawProfilerLinux PROPERTIES
    OUTPUT_NAME "Paw.Profiler.Linux"
)

target_compile_definitions(PawProfilerLinux PRIVATE
    HOST_AMD64
    PLATFORM_UNIX
    UNICODE
    PAL_STDCPP_COMPAT
    BIT64
)

target_compile_options(PawProfilerLinux PRIVATE
    -fms-extensions

    # include system headers first
    "-idirafter${CORECLR_REPO}/src/coreclr/pal/inc/rt"
    "-idirafter${CORECLR_REPO}/src/coreclr/pal/src/include"
    "-idirafter${CORECLR_REPO}/src/coreclr/pal/prebuilt/inc"
    "-idirafter${CORECLR_REPO}/src/coreclr/pal/inc"
    "-idirafter${CORECLR_REPO}/src/coreclr/inc"
    "-idirafter${CORECLR_REPO}/src/native"
)

target_include_directories(PawProfilerLinux PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}"
    "${PAW_PROFILER_SHARED_DIR}"

    "${CORECLR_REPO}/src/coreclr/pal/inc/rt"
    "${CORECLR_REPO}/src/coreclr/pal/src/include"
    "${CORECLR_REPO}/src/coreclr/pal/prebuilt/inc"
    "${CORECLR_REPO}/src/coreclr/pal/inc"
    "${CORECLR_REPO}/src/coreclr/inc"
    "${CORECLR_REPO}/src/native"
)

target_link_options(PawProfilerLinux PRIVATE
    -Wl,--no-undefined
)

target_link_libraries(PawProfilerLinux PRIVATE
    dl
    pthread
)