@ECHO OFF
YBuild\libgen.py GCODEGENERATOR --solution-name "GCodeGenerator" --release -G "NMake Makefiles/VS17" --no-3rdparty-update
