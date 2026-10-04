using System.Numerics;
using EscapeMine;
using Vortice.Direct2D1;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;
    private readonly GameSession game = new();
    private readonly Dictionary<string, G2Texture> textures = new();
    private G2Font? text, heading, small;
    private ID2D1SolidColorBrush? brush;
    private GameAudio? audio;
    private bool muted;
    private static readonly Color4 White = new(.94f, .96f, 1, 1);
    private static readonly Color4 Gold = new(1, .77f, .32f, 1);
    private static readonly Color4 Cyan = new(.30f, .89f, 1, 1);

    protected override void Initialize()
    {
        foreach (string name in new[] { "InplayBG", "Robby", "Stone_Tile", "HardStone_Tile", "pickaxe", "oxygen", "win", "Lose" })
            textures.Add(name, new G2Texture($"resource/{name}.png"));
        text = new G2Font("Malgun Gothic", 24);
        small = new G2Font("Malgun Gothic", 18);
        heading = new G2Font("Malgun Gothic", 54);
        brush = RenderTarget.CreateSolidColorBrush(White);
        audio = new GameAudio();
        game.Sound += audio.Play;
    }

    protected override void Update()
    {
        if (Input.IsKeyDown(Keys.M)) { muted = !muted; audio!.Muted = muted; }
        if (Input.IsKeyDown(Keys.Escape) && game.Phase is GamePhase.Result or GamePhase.Title) { Close(); return; }
        // 포커스를 잃으면 입력 및 시간을 정지합니다.
        if (!IsActive) { audio!.Pause(); return; }
        game.Tick((float)DeltaTime, new GameInput(Input.MousePosition.X,
            Input.IsKeyDown(Keys.Space), Input.IsButtonDown(MouseButtons.Left),
            Input.IsButtonDown(MouseButtons.Right), Input.IsKeyDown(Keys.R)));
        audio!.Update(game.Phase);
    }

    protected override void Render()
    {
        // 게임의 논리 좌표는 1200x900이며 Framework가 실제 Window에 맞춰 변환합니다.
        Image(game.Phase == GamePhase.Title ? "Robby" : "InplayBG", new Rect(0, 0, 1200, 900));
        Fill(new Rect(0, 0, 1200, 900), new Color4(.03f, .04f, .07f, .24f));
        if (game.Phase == GamePhase.Title) { Title(); return; }
        Field();
        Paddle();
        brush!.Color = game.Ball.Power > 0 ? Cyan : White;
        RenderTarget.FillEllipse(new Ellipse(game.Ball.Position, Rules.Radius, Rules.Radius), brush);
        brush.Color = White;
        RenderTarget.DrawEllipse(new Ellipse(game.Ball.Position, Rules.Radius + 4, Rules.Radius + 4), brush, 1.5f);
        Hud();
        if (game.FeedbackTime > 0) Label(game.Feedback, 410, 622, 500, Gold);
        if (game.Phase == GamePhase.Ready)
        {
            Fill(new Rect(230, 615, 740, 62), new Color4(.03f, .04f, .08f, .88f));
            Label("좌클릭으로 발사 · 우클릭으로 강타 준비", 285, 632, 700, White);
        }
        if (game.Phase == GamePhase.Result) Result();
        if (!IsActive && game.Phase == GamePhase.Play)
        {
            Fill(new Rect(260, 355, 680, 100), new Color4(.025f, .035f, .06f, .95f));
            Label("일시 정지 · 게임 창으로 돌아오면 계속됩니다", 300, 390, 650, White);
        }
    }

    private void Title()
    {
        Fill(new Rect(110, 120, 980, 670), new Color4(.025f, .035f, .06f, .91f));
        heading!.DrawText("ESCAPE MINE", new Rect(210, 164, 850, 90), Gold);
        Label("산소가 다하기 전에, 광산의 천장을 뚫으세요.", 215, 270, 850, White);
        Label("목표  최상단의 돌을 하나 파괴하면 탈출 성공", 215, 346, 850, Cyan);
        Label("마우스 이동   곡괭이 이동", 215, 410, 850, White);
        Label("좌클릭           스윙 / 공 타격 (POWER 1)", 215, 452, 850, White);
        Label("우클릭           강타 전환 (POWER 2 · 산소 2.5 소모)", 215, 494, 850, White);
        Label("공을 놓쳐도 바닥에서 반사됩니다. 헛스윙도 산소를 소모합니다.", 215, 558, 850, Gold, true);
        Label("SPACE  시작", 450, 645, 500, Cyan);
        Label($"세션 최고 점수 {game.Best:N0}    |    M 소리 ON/OFF    |    ESC 종료", 260, 725, 850, White, true);
    }

    private void Field()
    {
        Label("▲ 최상단 돌을 파괴하면 탈출", 126, 14, 600, Gold);
        Label($"남은 돌 {game.Rocks.Count(r => r.Health > 0)}", 864, 14, 230, White);
        foreach (Rock rock in game.Rocks)
        {
            if (rock.Health == 0 && !rock.Tank) continue;
            Rect dest = new(rock.X, rock.Y, Rules.Tile, Rules.Tile);
            if (rock.Tank)
            {
                Fill(dest, new Color4(.05f, .27f, .3f, .8f));
                Image("oxygen", new Rect(rock.X + 8, rock.Y + 8, 48, 48));
                continue;
            }
            int tile = (rock.Row * Rules.Columns + rock.Column) % 16;
            int size = rock.Hard ? 128 : 256;
            textures[rock.Hard ? "HardStone_Tile" : "Stone_Tile"].Draw(dest,
                new Rect(tile % 4 * size, tile / 4 * size, size, size));
            if (rock.Row == 0) Outline(dest, Gold, 2);
            if (rock.Hard && rock.Health == 1)
            {
                // 전용 Resource 미제공: 기존 돌 위에 균열을 겹쳐 손상 상태를 표시합니다.
                brush!.Color = Gold;
                Vector2 p = new(rock.X + 35, rock.Y + 4);
                Vector2 q = new(rock.X + 22, rock.Y + 28);
                Vector2 r = new(rock.X + 40, rock.Y + 42);
                RenderTarget.DrawLine(p, q, brush, 3);
                RenderTarget.DrawLine(q, r, brush, 3);
                RenderTarget.DrawLine(r, new(rock.X + 27, rock.Y + 62), brush, 3);
            }
            Label(rock.Health.ToString(), rock.X + 5, rock.Y + 39, 40, White, true);
        }
    }

    private void Paddle()
    {
        var p = game.Pickaxe;
        bool strong = p.Strong || (p.Flash > 0 && p.LastStrong);
        Color4 color = strong ? Gold : Cyan;
        // 실제 판정 범위를 표시하여 이미지의 투명 여백과 관계없이 타이밍을 읽을 수 있습니다.
        Outline(new Rect(p.X - p.HalfWidth, Rules.PaddleY - 64, p.HalfWidth * 2, 128),
            new Color4(color.R, color.G, color.B, p.Flash > 0 ? .9f : .3f), p.Flash > 0 ? 3 : 1);
        Matrix3x2 original = RenderTarget.Transform;
        float rotation = strong ? -.75f : .75f;
        if (p.Flash > 0) rotation += (p.Flash / .18f - .5f) * .8f;
        RenderTarget.Transform = Matrix3x2.CreateRotation(rotation, new(p.X, Rules.PaddleY)) * original;
        Image("pickaxe", new Rect(p.X - 48, Rules.PaddleY - 48, 96, 96));
        RenderTarget.Transform = original;
        Fill(new Rect(p.X - p.HalfWidth, Rules.PaddleY + 30, p.HalfWidth * 2, 5), color);
    }

    private void Hud()
    {
        Fill(new Rect(0, 810, 1200, 90), new Color4(.025f, .035f, .06f, .97f));
        Label($"O₂ {game.Oxygen:0.0}%", 22, 820, 220, game.Oxygen <= 20 ? Gold : Cyan);
        for (int i = 0; i < 10; i++)
        {
            Rect cell = new(190 + i * 28, 828, 23, 22);
            Fill(cell, new Color4(.18f, .21f, .26f, 1));
            float fraction = Math.Clamp((game.Oxygen - i * 10) / 10, 0, 1);
            if (fraction > 0) Fill(new Rect(cell.X, cell.Y, cell.Width * fraction, cell.Height), game.Oxygen <= 20 ? Gold : Cyan);
        }
        Label($"점수 {game.Score:N0}    시간 {(int)game.Elapsed / 60:00}:{(int)game.Elapsed % 60:00}", 510, 820, 660, White);
        Label($"{(game.Pickaxe.Strong ? "강타 준비 · 산소 2.5" : "일반 타격 · 산소 1")}   |   POWER {game.Ball.Power}   |   우클릭 전환", 22, 864, 900, Gold, true);
        Label(!audio!.Available ? "오디오 장치 없음" : muted ? "M 소리 OFF" : "M 소리 ON", 1010, 864, 180, White, true);
    }

    private void Result()
    {
        Fill(new Rect(0, 0, 1200, 900), new Color4(.02f, .025f, .04f, .82f));
        Fill(new Rect(270, 140, 660, 600), new Color4(.04f, .055f, .08f, .98f));
        Image(game.Won ? "win" : "Lose", new Rect(510, 166, 180, 180));
        heading!.DrawText(game.Won ? "탈출 성공!" : "산소 소진", new Rect(400, 358, 540, 80), game.Won ? Cyan : Gold);
        Label($"최종 점수   {game.Score:N0}", 410, 466, 500, White);
        Label($"플레이 시간   {(int)game.Elapsed / 60:00}:{(int)game.Elapsed % 60:00}", 410, 512, 500, White);
        Label($"세션 최고   {game.Best:N0}", 410, 558, 500, Gold);
        Label("R  타이틀로 돌아가기     ESC  종료", 363, 659, 600, Cyan);
    }

    private void Image(string name, Rect destination) => textures[name].Draw(destination, textures[name].SourceBounds);
    private void Fill(Rect rect, Color4 color) { brush!.Color = color; RenderTarget.FillRectangle(rect, brush); }
    private void Outline(Rect rect, Color4 color, float width) { brush!.Color = color; RenderTarget.DrawRectangle(rect, brush, width); }
    private void Label(string value, float x, float y, float width, Color4 color, bool tiny = false) =>
        (tiny ? small : text)!.DrawText(value, new Rect(x, y, width, 42), color);

    public override void Dispose()
    {
        if (audio != null) { game.Sound -= audio.Play; audio.Dispose(); }
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear();
        text?.Dispose(); small?.Dispose(); heading?.Dispose(); brush?.Dispose();
        base.Dispose();
    }
}
