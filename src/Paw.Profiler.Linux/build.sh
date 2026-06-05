#!/usr/bin/env bash

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

SOURCE_DIR="$SCRIPT_DIR"
BUILD_DIR="$SCRIPT_DIR/build"
CORECLR_DIR="$(cd -- "$SCRIPT_DIR/../../../runtime" && pwd)"

echo "Source dir:  $SOURCE_DIR"
echo "Build dir:   $BUILD_DIR"
echo "CoreCLR dir: $CORECLR_DIR"

if [ -d "$BUILD_DIR" ]; then
    rm $BUILD_DIR -R
fi

echo "== config =="

cmake -S $SOURCE_DIR \
      -B $BUILD_DIR \
      -G Ninja \
      -DCORECLR_REPO=$CORECLR_DIR

echo "== build =="

cmake --build $BUILD_DIR

echo "== success =="