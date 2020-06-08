#!/usr/bin/env bash
error() {
    local SOURCE_FILE=$1
    local LINE_NO=$2
    echo "ERROR: ${SOURCE_FILE}(${LINE_NO}):"
}

function isLinux() {
    [[ "$(uname -s)" == "Linux" ]]
}

function isMacOS() {
    [[ "$(uname -s)" == "Darwin" ]]
}

function isWindows() {
    ! (isLinux || isMacOS)
}

function cleanUnity() {
    rm -rf "${1}/Library"
    rm -rf "${1}/Temp"
}

function traceStart() {
    tracename=${1//\;}
    echo "## imp-ci group-start ${tracename}"
    export _TRACESTACK="${tracename};${_TRACESTACK-}"
}

function traceEnd() {
    top=${_TRACESTACK%%\;*}
    echo "## imp-ci group-end ${top}"
    _TRACESTACK=${_TRACESTACK#*\;}
}

traceStart "Sourcing pinned tools :round_pushpin:"
    # Ensure for the Mac TC agents that dotnet is on the path.
    if isMacOS; then
        if ! which dotnet; then
            export PATH="${PATH}:/usr/local/share/dotnet/"
        fi
    fi

    # Print the .NETCore version to aid debugging,
    # as well as ensuring that later calls to the tool don't print the welcome message on first run.
    if ! isLinux; then
        dotnet --version

        DOTNET_VERSION="$(dotnet --version)"

        if isWindows; then
            export MSBuildSDKsPath="${PROGRAMFILES}/dotnet/sdk/${DOTNET_VERSION}/Sdks"
        fi
    fi
traceEnd

# Creates an assembly name based on an argument (used as a prefix) and the current Git hash.
function setAssemblyName() {
    # Get first 8 characters of current git hash.
    GIT_HASH="$(git rev-parse HEAD | cut -c1-8)"

    if [ "$#" -ne 1 ]; then
        echo "'setAssemblyName' expects only one argument."
        echo "Example usage: 'setAssemblyName <assembly prefix>'"
        exit 1
    fi

    ASSEMBLY_NAME="${1}_${GIT_HASH}"
}

# Uploads an assembly, given an assembly prefix and a project name
function uploadAssembly() {
    if [ "$#" -ne 2 ]; then
        echo "'uploadAssembly' expects two arguments."
        echo "Example usage: 'uploadAssembly <assembly prefix> <project name>'"
        exit 1
    fi

    setAssemblyName "${1}"

    traceStart "Uploading assembly :outbox_tray:"
        spatial cloud upload "${ASSEMBLY_NAME}" --log_level=debug --force --enable_pre_upload_check=false --project_name="${2}"
    traceEnd "Uploading assembly :outbox_tray:"
}

function getAcceleratorArgs() {
    echo ${ACCELERATOR_ENDPOINT:+-adb2 -enableCacheServer -cacheServerEnableDownload true -cacheServerEnableUpload true -cacheServerEndpoint "${ACCELERATOR_ENDPOINT}"}
}
