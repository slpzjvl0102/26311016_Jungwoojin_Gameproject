using EscapeMine;
using System.Numerics;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
GameSession Playing(bool strong = false)
{
    var s = new GameSession(42);
    s.Tick(0, new(600, Start: true));
    s.Tick(0, new(600, Swing: true, TogglePower: strong));
    return s;
}
void Advance(GameSession s, float seconds)
{
    for (int i = 0; i < (int)(seconds * 240); i++) s.Tick(Rules.Step, new(s.Pickaxe.X));
}
void Aim(GameSession s, Rock r, int power)
{
    s.Ball.Position = new(r.X + 32, r.Y + Rules.Tile + Rules.Radius + 1);
    s.Ball.Velocity = new(0, -Rules.Speed); s.Ball.Power = power;
}
var ready = new GameSession(1);
ready.Tick(0, new(600, Start: true)); Advance(ready, 10);
Check(ready.Phase == GamePhase.Ready && ready.Elapsed == 0 && ready.Oxygen == 100, "Ready freezes oxygen and time");
Check(ready.Rocks.Count == 112 && ready.Rocks.Where(r => r.Row == 0).All(r => r.Health > 0), "Field and escape row");
var normal = Playing();
Check(normal.Ball.Power == 1 && normal.Oxygen == 99 && normal.Ball.Velocity.Y < 0, "Normal launch");
var strong = Playing(true);
Check(strong.Ball.Power == 2 && strong.Oxygen == 97.5f && !strong.Pickaxe.Strong, "Strong launch cost and auto reset");
Check(Math.Abs(strong.Ball.Velocity.X) < Math.Abs(normal.Ball.Velocity.X), "Strong shot is narrower");
strong.Ball.Position = new(50, 500);
strong.Tick(0, new(600, Swing: true, TogglePower: true));
Check(strong.Oxygen == 95 && !strong.Pickaxe.Strong, "Strong miss consumes oxygen and mode");
var damage = Playing(); damage.Rocks.Clear();
var hard = new Rock { Row = 3, Column = 4, Health = 2, Hard = true };
damage.Rocks.Add(hard); Aim(damage, hard, 1); Advance(damage, .05f);
Check(hard.Health == 1 && damage.Ball.Power == 0 && damage.Ball.Velocity.Y > 0 && damage.Score == 0, "Partial hard rock hit and reflection");
Aim(damage, hard, 0); Advance(damage, .05f);
Check(hard.Health == 1 && damage.Ball.Velocity.Y > 0, "Zero power cannot damage");
Aim(damage, hard, 1); Advance(damage, .05f);
Check(hard.Health == 0 && damage.Score == 40, "Hard rock destruction score");
var chain = Playing(true); chain.Rocks.Clear();
var lower = new Rock { Row = 4, Column = 6, Health = 1 };
var upper = new Rock { Row = 3, Column = 6, Health = 1 };
chain.Rocks.AddRange([upper, lower]); Aim(chain, lower, 2); Advance(chain, .2f);
Check(lower.Health == 0 && upper.Health == 0 && chain.Ball.Power == 0 && chain.Score == 40 && chain.Ball.Velocity.Y > 0, "Power 2 pierces two normal rocks");
var tankGame = Playing(true); tankGame.Rocks.Clear();
Advance(tankGame, 50);
var tank = new Rock { Row = 3, Column = 6, Tank = true };
tankGame.Rocks.Add(tank); Aim(tankGame, tank, 2); Advance(tankGame, .05f);
Check(!tank.Tank && tankGame.Oxygen > 99 && tankGame.Ball.Power == 2 && tankGame.Ball.Velocity.Y < 0, "Tank caps oxygen and preserves power/direction");
var floor = Playing(); floor.Rocks.Clear();
floor.Ball.Position = new(600, Rules.Floor - Rules.Radius - 1); floor.Ball.Velocity = new(0, Rules.Speed);
Advance(floor, .05f);
Check(floor.Phase == GamePhase.Play && floor.Ball.Velocity.Y < 0, "Floor bounce does not end run");
var win = Playing(); win.Rocks.Clear();
var exit = new Rock { Row = 0, Column = 4, Health = 1 }; win.Rocks.Add(exit);
Aim(win, exit, 1); Advance(win, .05f);
Check(win.Phase == GamePhase.Result && win.Won && win.Score > 1900 && win.Best == win.Score, "Exit wins and awards oxygen bonus");
int best = win.Best; float time = win.Elapsed; Advance(win, 2);
Check(win.Elapsed == time && win.Score == best, "Result freezes run and bonus is awarded once");
win.Tick(0, new(600, Restart: true)); win.Tick(0, new(600, Start: true));
Check(win.Phase == GamePhase.Ready && win.Oxygen == 100 && win.Score == 0 && win.Elapsed == 0 && win.Best == best && win.Rocks.Count == 112, "Restart resets run but retains session best");
var loss = Playing(); loss.Rocks.Clear(); Advance(loss, 401);
Check(loss.Phase == GamePhase.Result && !loss.Won && loss.Oxygen == 0, "Natural oxygen exhaustion loses");
var spam = Playing(); spam.Ball.Position = new(30, 400);
for (int i = 0; i < 100; i++) spam.Tick(0, new(600, Swing: true));
Check(spam.Phase == GamePhase.Result && !spam.Won, "Swing exhaustion ends immediately");
var edges = Playing(); edges.Rocks.Clear();
edges.Ball.Position = new(12, 500); edges.Ball.Velocity = new(-510, 0); Advance(edges, .05f);
Check(edges.Ball.Position.X >= 11 && edges.Ball.Velocity.X > 0, "Left wall correction");
edges.Ball.Position = new(1188, 500); edges.Ball.Velocity = new(510, 0); Advance(edges, .05f);
Check(edges.Ball.Position.X <= 1189 && edges.Ball.Velocity.X < 0, "Right wall correction");
Console.WriteLine($"{passed} rule checks passed.");

// 실제 필드를 끝까지 진행: 곡괭이가 공을 따라가며 내려오는 공을 강타합니다.
int wins = 0;
for (int seed = 0; seed < 20; seed++)
{
    var run = new GameSession(seed);
    run.Tick(0, new(600, Start: true)); run.Tick(0, new(600, Swing: true, TogglePower: true));
    for (int frame = 0; frame < 600 * 120 && run.Phase == GamePhase.Play; frame++)
    {
        bool hit = run.Ball.Velocity.Y > 0 && Math.Abs(run.Ball.Position.Y - Rules.PaddleY) < 40;
        run.Tick(1f / 120, new(run.Ball.Position.X, Swing: hit, TogglePower: hit));
        if (!float.IsFinite(run.Ball.Position.X) || !float.IsFinite(run.Ball.Position.Y))
            throw new Exception("Non-finite physics state");
    }
    Check(run.Phase == GamePhase.Result, $"Seed {seed} full run reaches a result");
    if (run.Won) wins++;
}
Check(wins > 0, "Full generated field is winnable");
Console.WriteLine($"{passed} total checks; autoplay won {wins}/20 runs.");
