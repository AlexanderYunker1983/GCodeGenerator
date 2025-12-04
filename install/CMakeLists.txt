# Файл сборки инсталляторов GCodeGenerator
set(Y_INSTALLER_SCRIPT "GCodeGenerator")

YBUILD_ADD_INSTALLER_NSIS(
        Installer
        "${Y_INSTALLER_SCRIPT}"
        BOOTSTRAPPER_SCRIPT
            bootstrapper.nsi)
        
add_dependencies(Installer GCodeGenerator)
