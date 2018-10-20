import subprocess

def ExecuteCommand(args, cwd=None):
	subprocess.Popen(args, cwd=cwd).wait()


def InitAndBuildSubService(baseDir, fullDir):
    cmds0 = [
        ['git', 'submodule', 'init'],
        ['git', 'submodule', 'update']
    ]

    cmds1 = [
        ['dotnet', 'build']
    ]

    for cmd in cmds0:
	       ExecuteCommand(cmd, cwd=baseDir)
    for cmd in cmds1:
            ExecuteCommand(cmd, cwd=fullDir)

def InitAndBuildEverything():
    services = ['MDACSApp', 'MDACSDatabase', 'MDACSCommand', 'MDACSAuth']
    for service in services:
        InitAndBuildSubService(
            './%s' % service,
            './%s/%s' % (service, service)
        )

InitAndBuildEverything()
