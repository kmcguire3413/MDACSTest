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

def InitMDACSAppJSXCompiler():
    ExecuteCommand(
        [ 'npm', 'install', '--save-dev', 'babel-cli' ],
        cwd='./MDACSApp/MDACSApp'
    )

    ExecuteCommand(
        [ 'npm', 'install', '--save-dev', 'babel-plugin-transform-react-jsx' ],
        cwd='./MDACSApp/MDACSApp'
    )

def InitAndBuildEverything():
    services = ['MDACSApp', 'MDACSDatabase', 'MDACSCommand', 'MDACSAuth']

    InitMDACSAppJSXCompiler()

    for service in services:
        InitAndBuildSubService(
            './%s' % service,
            './%s/%s' % (service, service)
        )

InitAndBuildEverything()
