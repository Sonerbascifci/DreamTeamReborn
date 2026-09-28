// M6: the console entry point now takes arguments.
//
// D33 (M2) said "this entry point takes no arguments; it plays one fixed fictional
// fixture". That constraint belonged to M2, when there was no batch mode. M6 lifts
// it: 09's CLI contract requires `single` and `batch` sub-commands.
//
// The argument-less invocation is NOT removed. It still plays
// `single --fixture neutral-mirror --seed 20260927` and produces byte-identical
// output, which `TheArgumentlessInvocationMatchesTheSingleCommand` tests.
//
// All behaviour lives in SimulatorRunner so it is testable without spawning a
// process. This file only wires stdout/stderr and the exit code.

using DreamTeam.Simulator;

var runner = new SimulatorRunner(Console.Out, Console.Error);
return runner.Run(args);
