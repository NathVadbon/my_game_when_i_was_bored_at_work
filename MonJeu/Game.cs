using Raylib_cs;
using System.Numerics;
using System;
using System.IO;
using System.Collections.Generic;

public class Game
{
    const int Width = 800;
    const int Height = 450;

    int frameCount = 0;
    List<Enemy> enemies = new List<Enemy>();

    struct Bomb
    {
        public Vector2 position;
        public bool active;
        public float timer;
        public float countdown;
        public float explosionTimer;
    }

    List<Bomb> bombs = new List<Bomb>();
    float bombeIntervalle = 5f;
    float bombeExplosion = 3f;
    float bombAddTimer = 0f;
    float bombAddInterval = 30f;
    int maxBombs = 3;

    float enemyAddTimer = 0f;
    float enemyAddInterval = 8f;

    enum ItemType { Coin, Heart, Shield, Rainbow }
    struct Item
    {
        public Vector2 pos;
        public ItemType type;
        public float lifetime;
    }

    List<Item> items = new List<Item>();
    float itemSpawnTimer = 0f;
    float itemSpawnInterval = 6f; // attempt spawn every 6s

    bool shieldActive = false;
    int shieldHits = 0;

    Random rng = new Random();

    float invincibilite = 0f;
    float rainbowTimer = 0f;

    bool isGameOver = false;

    int playerScore = 0;
    Color scoreColor = Color.White;
    int highScore = 0;
    string highScoreFilePath = string.Empty;

    Vector2 player = new Vector2(400, 225);
    Color playerColor = Color.White;
    int playerHeight = 20;
    int playerWidth = 20;

    Rectangle[] playerHealth = new Rectangle[3];
    Color[] playerHealthColors = { Color.White, Color.White, Color.White };

    float speed = 4f;
    float baseEnemySpeed = 3f;
    float enemySpeedMultiplier = 1f;
    float speedIncreaseInterval = 5f; // every 5s increase
    float speedIncreaseTimer = 0f;
    float speedIncreaseFactor = 1.02f; // 2% per interval
    float speedMultiplierCap = 1.45f; // +60% cap

    // Spawning now happens when an enemy crosses a border.
    float spawnCooldown = 3.5f;
    float spawnCooldownTimer = 0f;

    int maxEnemies = 15;

    int livesIndex = 2;

    void InitializeGame()
    {
        InitializePlayerHealth();

        Raylib.InitWindow(Width, Height, "Apprendre C# - Jeu simple");
        Raylib.SetTargetFPS(60);

        highScoreFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MonJeu", "highscore.txt");
        LoadHighScore();
        AddEnemy();
    }

    void InitializePlayerHealth()
    {
        playerHealth[0] = new Rectangle(10, 10, 10, 20);
        playerHealth[1] = new Rectangle(30, 10, 10, 20);
        playerHealth[2] = new Rectangle(50, 10, 10, 20);
    }

    void UpdateGameState(float dt)
    {
        invincibilite -= dt;
        UpdateRainbowTimer(dt);
        spawnCooldownTimer -= dt;
        if (spawnCooldownTimer < 0f) spawnCooldownTimer = 0f;

        UpdateEnemySpeed(dt);
        UpdateEnemySpawns(dt);
        UpdateBombSpawns(dt);

        Input();
        UpdateBombs(dt);
        UpdateItemSpawns(dt);
        UpdateItems(dt);
        UpdateEnemy();
        CollisionWindow();
    }

    void UpdateRainbowTimer(float dt)
    {
        if (rainbowTimer > 0f)
        {
            rainbowTimer -= dt;
            if (rainbowTimer <= 0f) rainbowTimer = 0f;
        }
    }

    void UpdateEnemySpeed(float dt)
    {
        speedIncreaseTimer += dt;
        if (speedIncreaseTimer >= speedIncreaseInterval)
        {
            enemySpeedMultiplier = Math.Min(speedMultiplierCap, enemySpeedMultiplier * speedIncreaseFactor);
            speedIncreaseTimer = 0f;
        }
    }

    void UpdateEnemySpawns(float dt)
    {
        enemyAddTimer += dt;
        if (enemyAddTimer >= enemyAddInterval)
        {
            AddEnemy();
            enemyAddTimer = 0f;
        }
    }

    void UpdateBombSpawns(float dt)
    {
        bombAddTimer += dt;
        if (bombAddTimer >= bombAddInterval && bombs.Count < maxBombs)
        {
            AddBomb();
            bombAddTimer = 0f;
        }
    }

    void UpdateItemSpawns(float dt)
    {
        itemSpawnTimer += dt;
        if (itemSpawnTimer >= itemSpawnInterval)
        {
            SpawnItem();
            itemSpawnTimer = 0f;
        }
    }

    public void Run()
    {
        InitializeGame();

        while (!Raylib.WindowShouldClose())
        {
            if (!isGameOver)
            {
                ++frameCount;
                float dt = Raylib.GetFrameTime();
                UpdateGameState(dt);
            }

            Draw();

            if (isGameOver && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Restart();
            }
        }

        Raylib.CloseWindow();
    }

    void Input()
    {
        if (Raylib.IsKeyDown(KeyboardKey.Right)) player.X += speed;
        if (Raylib.IsKeyDown(KeyboardKey.Left)) player.X -= speed;
        if (Raylib.IsKeyDown(KeyboardKey.Up)) player.Y -= speed;
        if (Raylib.IsKeyDown(KeyboardKey.Down)) player.Y += speed;
    }

    void CollisionWindow()
    {
        if (player.X < 0) player.X = Width - 20;
        if (player.X > Width - 20) player.X = 0;
        if (player.Y < 0) player.Y = Height - 20;
        if (player.Y > Height - 20) player.Y = 0;
    }

    void UpdateEnemy()
    {
        Rectangle playerRect = new Rectangle(player.X, player.Y, playerWidth, playerHeight);

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];

            Raylib.DrawTriangleLines(e.sommetHaut, e.coinGauche, e.coinDroit, Color.White);

            float effSpeed = baseEnemySpeed * enemySpeedMultiplier;
            e.sommetHaut += e.direction * effSpeed;
            e.coinGauche += e.direction * effSpeed;
            e.coinDroit  += e.direction * effSpeed;

            // If enemy crosses any border, respawn it at a random side with a new path
            // and spawn an additional enemy immediately.
            if (e.sommetHaut.X < 0 || e.sommetHaut.X > Width - 20 || e.sommetHaut.Y < 0 || e.sommetHaut.Y > Height - 20)
            {
                int side = rng.Next(1, 5);
                float nx = rng.Next(0, Width - 20);
                float ny = rng.Next(0, Height - 20);
                // Respawn current enemy
                e = CreateEnemyForSide(side, nx, ny);

                // Spawn an extra enemy if cooldown elapsed
                if (spawnCooldownTimer <= 0f)
                {
                        int side2 = rng.Next(1, 5);
                        // avoid same side as the respawned enemy
                        if (side2 == side) side2 = (side2 % 4) + 1;
                        float nx2 = rng.Next(0, Width - 20);
                        float ny2 = rng.Next(0, Height - 20);
                        if (enemies.Count < maxEnemies)
                        {
                            enemies.Add(CreateEnemyForSide(side2, nx2, ny2));
                        }
                    spawnCooldownTimer = spawnCooldown;
                }
            }

            if ((Raylib.CheckCollisionPointRec(e.sommetHaut, playerRect) ||
                Raylib.CheckCollisionPointRec(e.coinGauche, playerRect) ||
                Raylib.CheckCollisionPointRec(e.coinDroit, playerRect)) &&
                invincibilite <= 0f)
            {
                if (shieldActive && shieldHits > 0)
                {
                    shieldHits--;
                    if (shieldHits <= 0) shieldActive = false;
                    invincibilite = 0.5f;
                }
                else
                {
                    HealthUpdate();
                    invincibilite = 1.5f;
                }
            }

            enemies[i] = e;
        }
    }

    void AddEnemy()
    {
        if (enemies.Count >= maxEnemies) return;

        float x = rng.Next(0, Width - 20);
        float y = rng.Next(0, Height - 20);

        int sideSpawn = rng.Next(1,5);

        switch (sideSpawn)
        {
            case 1: // Top
                enemies.Add(CreateEnemyForSide(1, x, y));
                break;
            case 2: // Right
                enemies.Add(CreateEnemyForSide(2, x, y));
                break;
            case 3: // Left
                enemies.Add(CreateEnemyForSide(3, x, y));
                break;
            case 4: // Bottom
                enemies.Add(CreateEnemyForSide(4, x, y));
                break;
        }
    }

    void AddBomb()
    {
        float timerOffset = (float)rng.NextDouble() * 1.5f;
        bombs.Add(new Bomb
        {
            active = false,
            timer = timerOffset,
            countdown = 0f,
            explosionTimer = 0f,
            position = Vector2.Zero
        });
    }

    void UpdateBombs(float dt)
    {
        for (int i = 0; i < bombs.Count; i++)
        {
            Bomb bomb = bombs[i];
            bomb.timer += dt;

            if (!bomb.active && bomb.timer >= bombeIntervalle)
            {
                bomb.active = true;
                bomb.timer = 0f;
                bomb.countdown = bombeExplosion;
                bomb.position = new Vector2(rng.Next(100, Width - 100), rng.Next(75, Height - 75));
            }

            if (bomb.active)
            {
                bomb.countdown -= dt;
                if (bomb.countdown <= 0f)
                {
                    float dist = Vector2.Distance(bomb.position, player);
                    if (dist < 65f && invincibilite <= 0f)
                    {
                        HealthUpdate();
                        invincibilite = 1.5f;
                    }
                    bomb.explosionTimer = 0.3f;
                    bomb.active = false;
                }
            }

            if (bomb.explosionTimer > 0f)
            {
                bomb.explosionTimer -= dt;
            }

            bombs[i] = bomb;
        }
    }

    void SpawnItem()
    {
        // pick item type by weighted probabilities: coin 65%, heart 20%, shield 13%, rainbow 2%
        double r = rng.NextDouble();
        ItemType type;
        if (r < 0.60) type = ItemType.Coin;
        else if (r < 0.80) type = ItemType.Heart;
        else if (r < 0.95) type = ItemType.Shield;
        else type = ItemType.Rainbow;

        Item it = new Item
        {
            pos = new Vector2(rng.Next(20, Width - 20), rng.Next(20, Height - 20)),
            type = type,
            lifetime = 12f
        };
        items.Add(it);
    }

    void UpdateItems(float dt)
    {
        Rectangle playerRect = new Rectangle(player.X, player.Y, playerWidth, playerHeight);
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            it.lifetime -= dt;
            bool picked = false;
            float pickupRadius = 18f;
            Vector2 playerCenter = new Vector2(player.X + playerWidth / 2f, player.Y + playerHeight / 2f);
            if (Vector2.Distance(it.pos, playerCenter) <= pickupRadius)
            {
                ApplyItemEffect(it.type);
                picked = true;
            }

            if (picked || it.lifetime <= 0f)
            {
                items.RemoveAt(i);
            }
            else
            {
                items[i] = it;
            }
        }
    }

    void ApplyItemEffect(ItemType type)
    {
        switch (type)
        {
            case ItemType.Coin:
                playerScore += 100;
                if (playerScore > highScore) highScore = playerScore;
                break;
            case ItemType.Heart:
                if (livesIndex < 2)
                {
                    livesIndex++;
                    if (livesIndex >= 0 && livesIndex < playerHealthColors.Length) playerHealthColors[livesIndex] = Color.White;
                }
                break;
            case ItemType.Shield:
                shieldActive = true;
                shieldHits = 2;
                break;
            case ItemType.Rainbow:
                invincibilite = 10f;
                rainbowTimer = 10f;
                // regen to max
                livesIndex = 2;
                for (int j = 0; j < playerHealthColors.Length; j++) playerHealthColors[j] = Color.White;
                break;
        }
    }

    void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);

        DrawHud();
        DrawPlayer();
        DrawHealth();
        DrawItems();
        DrawShieldAura();
        DrawBombs();
        DrawGameOver();

        Raylib.EndDrawing();
    }

    void DrawHud()
    {
        Raylib.DrawText(playerScore.ToString(), 750, 10, 25, scoreColor);
        Raylib.DrawText("High: " + highScore.ToString(), 600, 12, 20, Color.Gold);

        if (!isGameOver && frameCount % 20 == 0)
        {
            playerScore += 5;
        }
    }

    void DrawPlayer()
    {
        playerColor = GetPlayerColor();
        Raylib.DrawRectangle((int)player.X, (int)player.Y, playerWidth, playerHeight, playerColor);
    }

    Color GetPlayerColor()
    {
        if (invincibilite <= 0f) return Color.White;

        if (rainbowTimer > 0f)
        {
            Color[] rainbowColors = { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Purple };
            int rainbowIndex = (frameCount / 3) % rainbowColors.Length;
            return rainbowColors[rainbowIndex];
        }

        return (frameCount / 5) % 2 == 0 ? Color.Red : Color.DarkGray;
    }

    void DrawHealth()
    {
        for (int j = 0; j < playerHealth.Length; j++)
        {
            Raylib.DrawRectangle((int)playerHealth[j].X, (int)playerHealth[j].Y, (int)playerHealth[j].Width, (int)playerHealth[j].Height, playerHealthColors[j]);
        }
    }

    void DrawItems()
    {
        foreach (var it in items)
        {
            switch (it.type)
            {
                case ItemType.Coin:
                    Raylib.DrawCircle((int)it.pos.X, (int)it.pos.Y, 7f, Color.Gold);
                    Raylib.DrawCircleLines((int)it.pos.X, (int)it.pos.Y, 7f, Color.Orange);
                    Raylib.DrawLine((int)it.pos.X - 7, (int)it.pos.Y, (int)it.pos.X + 7, (int)it.pos.Y, Color.Orange);
                    Raylib.DrawLine((int)it.pos.X, (int)it.pos.Y - 7, (int)it.pos.X, (int)it.pos.Y + 7, Color.Orange);
                    break;
                case ItemType.Heart:
                    Raylib.DrawCircle((int)it.pos.X - 4, (int)it.pos.Y - 2, 5f, Color.Red);
                    Raylib.DrawCircle((int)it.pos.X + 4, (int)it.pos.Y - 2, 5f, Color.Red);
                    Raylib.DrawTriangle(
                        new Vector2(it.pos.X - 10, it.pos.Y - 3),
                        new Vector2(it.pos.X, it.pos.Y + 10),
                        new Vector2(it.pos.X + 10, it.pos.Y - 3),
                        Color.Red);
                    break;
                case ItemType.Shield:
                    Raylib.DrawCircle((int)it.pos.X, (int)it.pos.Y, 8f, Color.SkyBlue);
                    Raylib.DrawCircleLines((int)it.pos.X, (int)it.pos.Y, 8f, Color.Blue);
                    Raylib.DrawCircle((int)it.pos.X, (int)it.pos.Y, 4f, Color.White);
                    Raylib.DrawCircleLines((int)it.pos.X, (int)it.pos.Y, 4f, Color.Blue);
                    break;
                case ItemType.Rainbow:
                    float cx = it.pos.X;
                    float cy = it.pos.Y;
                    float outer = 10f;
                    float inner = 4f;
                    bool whiteFlash = (frameCount % 10) < 3;

                    Vector2[] pts = new Vector2[10];
                    for (int k = 0; k < 10; k++)
                    {
                        float angle = (float)(Math.PI / 5 * k) - (float)(Math.PI / 2);
                        float r = k % 2 == 0 ? outer : inner;
                        pts[k] = new Vector2(cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
                    }

                    Color[] couleurs = { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue };
                    int offset = (frameCount / 2) % couleurs.Length;

                    if (whiteFlash)
                    {
                        Raylib.DrawCircle((int)cx, (int)cy, 8f, Color.White);
                        Raylib.DrawCircleLines((int)cx, (int)cy, 8f, Color.SkyBlue);
                    }
                    else
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            Raylib.DrawTriangle(
                                new Vector2(cx, cy),
                                pts[(k * 2 + 2) % 10],
                                pts[k * 2],
                                couleurs[(k + offset) % couleurs.Length]);
                        }
                    }
                    break;
            }
        }
    }

    void DrawShieldAura()
    {
        if (shieldActive)
        {
            int cx = (int)(player.X + playerWidth / 2);
            int cy = (int)(player.Y + playerHeight / 2);
            Raylib.DrawCircleLines(cx, cy, 18f, Color.SkyBlue);
            Raylib.DrawText("" + shieldHits, cx + 22, cy - 8, 14, Color.SkyBlue);
        }
    }

    void DrawBombs()
    {
        foreach (Bomb bomb in bombs)
        {
            if (bomb.active)
            {
                float progression = 1f - (bomb.countdown / bombeExplosion);
                float vitesse = 0.4f - progression * 0.45f;
                bool visible = (int)(bomb.countdown / vitesse) % 2 == 0;

                if (visible)
                {
                    Raylib.DrawCircle((int)bomb.position.X, (int)bomb.position.Y, 10f, Color.White);
                }
            }

            if (bomb.explosionTimer > 0f)
            {
                float progression = 1f - (bomb.explosionTimer / 0.3f);
                float rayon = 20f + progression * 60f;
                byte alpha = (byte)(255 * (1f - progression));
                Color rayColor = new Color(255, 255, 255, (int)alpha);
                Raylib.DrawCircle((int)bomb.position.X, (int)bomb.position.Y, rayon, rayColor);
            }
        }
    }

    void DrawGameOver()
    {
        if (isGameOver)
        {
            scoreColor = Color.Black;
            Raylib.DrawText("Game Over", 300, 200, 40, Color.Red);
            Raylib.DrawText("YOUR SCORE :", 250, 150, 35, Color.White);
            Raylib.DrawText(playerScore.ToString(), 520, 150, 35, Color.White);
            Raylib.DrawText("Click to restart", 290, 260, 25, Color.White);
        }
    }

    void Restart()
    {
        player = new Vector2(400, 225);
        livesIndex = 2;
        playerHealthColors[0] = Color.White;
        playerHealthColors[1] = Color.White;
        playerHealthColors[2] = Color.White;
        playerScore = 0;
        scoreColor = Color.White;
        frameCount = 0;
        invincibilite = 0f;
        rainbowTimer = 0f;
        bombAddTimer = 0f;
        bombs.Clear();
        AddBomb();
        items.Clear();
        itemSpawnTimer = 0f;
        shieldActive = false;
        shieldHits = 0;
        enemies.Clear();
        AddEnemy();
        isGameOver = false;
    }

    void LoadHighScore()
    {
        if (string.IsNullOrEmpty(highScoreFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(highScoreFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(highScoreFilePath))
            {
                var text = File.ReadAllText(highScoreFilePath);
                if (int.TryParse(text, out int val)) highScore = val;
            }
        }
        catch { /* ignore read errors */ }
    }

    void SaveHighScore()
    {
        if (string.IsNullOrEmpty(highScoreFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(highScoreFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(highScoreFilePath, highScore.ToString());
        }
        catch { /* ignore write errors */ }
    }

    void HealthUpdate()
    {
        if (livesIndex >= 0)
        {
            playerHealthColors[livesIndex] = Color.Black;
            livesIndex--;
        }
        if (livesIndex < 0)
        {
            isGameOver = true;
            if (playerScore > highScore)
            {
                highScore = playerScore;
                SaveHighScore();
            }
        }
    }

    Enemy CreateEnemyForSide(int side, float x, float y)
    {
        switch (side)
        {
            case 1: // Top
            {
                float vx = (float)(rng.NextDouble() * 0.2 - 0.1); // -0.1..0.1
                Vector2 dir = new Vector2(vx, 1f);
                dir = Vector2.Normalize(dir);
                return new Enemy {
                    sommetHaut = new Vector2(x, 20),
                    coinGauche = new Vector2(x - 10, 0),
                    coinDroit  = new Vector2(x + 10, 0),
                    direction  = dir
                };
            }
            case 2: // Right
            {
                float vy = (float)(rng.NextDouble() * 0.2 - 0.1);
                Vector2 dir = new Vector2(-1f, vy);
                dir = Vector2.Normalize(dir);
                return new Enemy {
                    sommetHaut = new Vector2(Width - 40, y),
                    coinGauche = new Vector2(Width - 20, y - 10),
                    coinDroit  = new Vector2(Width - 20, y + 10),
                    direction  = dir
                };
            }
            case 3: // Left
            {
                float vy = (float)(rng.NextDouble() * 0.2 - 0.1);
                Vector2 dir = new Vector2(1f, vy);
                dir = Vector2.Normalize(dir);
                return new Enemy {
                    sommetHaut = new Vector2(20, y),
                    coinGauche = new Vector2(0, y - 10),
                    coinDroit  = new Vector2(0, y + 10),
                    direction  = dir
                };
            }
            default: // Bottom
            {
                float vx = (float)(rng.NextDouble() * 0.2 - 0.1);
                Vector2 dir = new Vector2(vx, -1f);
                dir = Vector2.Normalize(dir);
                return new Enemy {
                    sommetHaut = new Vector2(x, Height - 40),
                    coinGauche = new Vector2(x - 10, Height - 20),
                    coinDroit  = new Vector2(x + 10, Height - 20),
                    direction  = dir
                };
            }
        }
    }
}
