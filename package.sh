#!/bin/bash
# Copyright © 2013, Elián Hanisch
#
# This script is for packaging everything into a zip file.

NAME="DontUnload"
DIR="Package/$NAME"

mkdir -vp "$DIR/Sources"

cp -v "bin/Release/$NAME.dll" "$DIR"
cp -v *.cs "$DIR/Sources"
cp -v *.txt "$DIR"

cd Package
zip -r "$NAME.zip" "$NAME"
