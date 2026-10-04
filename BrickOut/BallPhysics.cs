using System.Numerics;

namespace EscapeMine;

// 이동과 충돌 기하만 처리합니다. 피해·점수·승패는 GameSession이 결정합니다.
internal static class BallPhysics
{
    public static int Move(Ball ball, float dt)
    {
        int bounces = 0;
        ball.Position += ball.Velocity * dt;
        if (ball.Position.X < Rules.Radius || ball.Position.X > Rules.Width - Rules.Radius)
        {
            ball.Position = new(Math.Clamp(ball.Position.X, Rules.Radius, Rules.Width - Rules.Radius), ball.Position.Y);
            ball.Velocity = new(-ball.Velocity.X, ball.Velocity.Y);
            bounces++;
        }
        if (ball.Position.Y < Rules.Radius || ball.Position.Y > Rules.Floor - Rules.Radius)
        {
            ball.Position = new(ball.Position.X, Math.Clamp(ball.Position.Y, Rules.Radius, Rules.Floor - Rules.Radius));
            ball.Velocity = new(ball.Velocity.X, -ball.Velocity.Y);
            bounces++;
        }
        return bounces;
    }

    public static bool Overlaps(Ball ball, Rock rock)
    {
        Vector2 nearest = new(Math.Clamp(ball.Position.X, rock.X, rock.X + Rules.Tile),
            Math.Clamp(ball.Position.Y, rock.Y, rock.Y + Rules.Tile));
        return Vector2.DistanceSquared(ball.Position, nearest) <= Rules.Radius * Rules.Radius;
    }

    public static void Reflect(Ball ball, Rock rock, Vector2 old)
    {
        // 240Hz 고정 step(이동 2.125px) + 진입 면 보정으로 관통/중복 접촉 방지.
        if (old.Y >= rock.Y + Rules.Tile)
        {
            ball.Position = new(ball.Position.X, rock.Y + Rules.Tile + Rules.Radius + .1f);
            ball.Velocity = new(ball.Velocity.X, Math.Abs(ball.Velocity.Y));
        }
        else if (old.Y <= rock.Y)
        {
            ball.Position = new(ball.Position.X, rock.Y - Rules.Radius - .1f);
            ball.Velocity = new(ball.Velocity.X, -Math.Abs(ball.Velocity.Y));
        }
        else
        {
            bool left = old.X < rock.X + Rules.Tile / 2;
            ball.Position = new(left ? rock.X - Rules.Radius - .1f : rock.X + Rules.Tile + Rules.Radius + .1f, ball.Position.Y);
            ball.Velocity = new(left ? -Math.Abs(ball.Velocity.X) : Math.Abs(ball.Velocity.X), ball.Velocity.Y);
        }
    }
}
