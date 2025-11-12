// Disable test parallelization because Spectre.Console doesn't support concurrent interactive displays
[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.ClassLevel)]
