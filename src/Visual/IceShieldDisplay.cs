using DeVect.Combat;
using UnityEngine;

namespace DeVect.Visual;

internal sealed class IceShieldDisplay
{
    private const int HudLayer = 27;
    private const float HealthHudStartViewportX = 0.124f;
    private const float HealthHudUnitViewportSpacing = 0.0295f;
    private const float HudViewportY = 0.92f;
    private const float MaskAnchorWorldOffsetX = 0.22f;
    private const float MaskAnchorWorldOffsetY = -0.01f;
    private const int PetalsPerLayer = IceShieldState.PetalsPerShield;
    private const int LayerCount = IceShieldState.MaxShieldLayers;
    private const int MaxPetals = IceShieldState.MaxPetals;
    private static readonly string[] HealthNameKeywords = { "health", "mask", "blue", "joni", "lifeblood", "hp" };
    private static readonly Color ActivePetalColor = new(0.78f, 0.94f, 1f, 0.98f);
    private static readonly Color InactivePetalColor = new(0.42f, 0.56f, 0.68f, 0.24f);
    private static readonly Vector3[] PetalOffsets =
    {
        new Vector3(0f, 0.18f, 0f),
        new Vector3(0.18f, 0f, 0f),
        new Vector3(0f, -0.18f, 0f),
        new Vector3(-0.18f, 0f, 0f)
    };

    // 花瓣sprite数组：上、右、下、左 (对应索引 0,1,2,3)
    private static Sprite?[] _petalSprites = new Sprite?[4];
    private static Sprite? _coreSprite;
    private static Sprite? _hudIconSprite;

    private GameObject? _root;
    private SpriteRenderer? _coreRenderer;
    private SpriteRenderer[] _petalRenderers = new SpriteRenderer[MaxPetals];

    // Base64 encoded petal images (ice crystal flower petals)
    // petal_up.png - 向上指的花瓣
    private const string petal_up_b64 = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAFc0lEQVR4nO2aT4gbVRzHP5PJxHRDk81upGWXXVyjIFIKRaUnDx7EWwUVqQdBSz1UiyJo/4AoatUqVtRLD4qgvQl6EMSjRU8FD6IHQUi3bG27trObTdoku5l58zxkXvMym3S3m5mJh/le9s3v/fv9eb/f771fFhIkSJAgQYIECRIkSJAgQYIECRIkSJAgRpy35W+j3D81ys1XmnJxlPvDCBWw0pSLy03+Ue1R8TESBaw5srEZWhxIx73hmiMbLYfrLYe6oqn2miMbd1hGLk5+YlWAK2S7LWgB7Mzzt9a1WGvxoJC4rpDttGlk4uLJiGsjJfw2i18VKTAkDdByeDhjsi0uJUQeA6SUcs2Rjbag1XZp0RE8KDyK3nZptQWtNUc2pJQyav4idwHh4ejf126wW3i4O/P8odMX6+w2U6Qz5uC5USBSF3CFbAsPR8iOZYXEmRjjnN8tAsNNgOUme00DK5Nmm2mQNlNYUbpDZC6gH9+AJQXrhV9H1+dE6QqRKEBKKSs2h4WH0xa0hIcrJI7wcBbr7LlUYxfdWOAC7qUauxbr7PFPjCM8XH+uIzycqJQQiQIqNocBLq5wpF//dIHf6VpcAMKnxY7Qg6CUUgoP58Iyr04VeH9AILt51Cs2f6p2LsND6wZKXLzu2oZhhBq3Qj0BSnjh4cyM89GgcZdqPAC4uvAAjTZfDJojPJyKzcthu0Jo2tSFh47l/O+b/q/a0wXOVWz+GrRWPsshM4VlGlhmqpMJLtd4Q/WXS3we1kkY1Wuw30XolpgZ5yTAXROcCpORUBSwGevr4xeq7N1oTT0bXK7xjpC4M+OcDDsrDH2MlPDQYfriCp+pvlKOA/rxB/Bd4Bc6MWAhuF4uwwsA6uhXm3yq+qYKvKkuR/4Ya1hXCNUFdOEB7AZfDRjqApRLzOrErMVzwYGlHK8D7NjO8XC47MVQ2tOtf2GZjweNy2fZr9wBQHi4s0V+1scsVHnETHXS8qrDWUUvjrFPD4YAYZ6CkQRBX/iem6BSiC48QLXJD1HysmUF3OKuvxn0fQ4HhVewG3wX3Cest0JoJ2BmnFf60fNZ9gdpC1UeRbN+xeZKxeYKcD4sfjaLoRTQYxGJO1XgJb2/OMazwTmrDidni/ykplVsrgWG9FWCyiJqr348bAVbDh7B9KeYCqY8bYzbaPOuml8ucR9AxWa5z/J36x+lHE9CNzVCeIEw1iCYtTgGYJm8Nr/EY2ziRlgcY1+UPMVeFs9aHBMe7twkP1ZsrpZLTFRsmlr/AZX24uAndgUoVGyu+n+XyyUmFqo8FZfQOkb201jK4HmAconC/BKPj4yPUW08N8n35RIF1R4VH5EeuWqTT1Q7l+GQ3je/xBNzk3zrt582B5ii2uQ91d6xnRNh8zjUCVBpCDppSaOn7QYf6mMbbU7r377wAhBKEUHUV3urSv9e7xRFAntZwXm3g1H+f4AY0AbWC69wucbRMJnYsgL0i0fPSdikReaXeAb/Kuy3bwv6Pv/L12Ap12upYAyYm+QMXRc4ExUfG2GoIGgYhqGuxGYKC6/3nl7KcVSvCJkGlqoJMMAFTKNj2XyWI/3cYGacU2FWhEKprAZrghdXeFH1Fcc4ob8HVJ1PeDhzk3wJML/EQTPlFz20W6BfEruZBWbGOwVRf+zQwkMECtCFV8hneQt6FTBb5DTdt0B6ocqhfgrQq0Eq+oepgFBigGEYhpnC6ic8QH2Vt4O0+SUO9msrxCE8xPwWaLS7hc2UwXH6/0ocK0LLAhtZpL7aqe4qeJIP8NNg0P/jsj6EnAbvudP4ekDXjX5E/xn8zWyR3RutHYXwEME9IKiEHds3/CnrLHB2usD9O/PcG+xUlZ8ohAf4D/hL1VB1pZcpAAAAAElFTkSuQmCC";

    
    // petal_right.png - 向右指的花瓣  
    private const string petal_right_b64 = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAFW0lEQVR4nO2ay4scRRzHP93VPc7DndmdbbNrNgtuBgQRJYf8AfHgScghgcRLVCTiI4JIgg8QRYgaEwQVjKCiQQRvXkUCCoqS4FVyyjghS0LiziQ7u0x2nZ6e8tBds9W989zsY3D7C8XUq3/1+367qvrX1QMxYsSIESNGjBgxYsSIEWP7wei3o5RSDmTYMPq2vZXo6WSUuNfE7dZfmNihAYZciK7OSSllL8K9oAQZViHaOqXuuk5+UCGiM0GY2MMoQsihfoh7kkY3g8LAmp3nE1V+IM+JVtsQzoaWI5fn5NMqPz3KWZXvRTiK61U+i9ZNj/IKrAgwTLPBgDB5helRzurk+1kCNxf5olObEgF8AYZFBLMdeYDZeV7ymrgqqXqvSaNT6jZQVEyviXt5Th4b9PG63rC6NSpS5Rofqjonw+trGchr4mL6eWFgXbnFqwDFMi9LKeVWzQazVwedvCp7ErddGktzpJ0NJ8OzwSzxZ5SksTPH+wDTo5z2mrgNT9bXh9JgMCdG+Khdw1iak55sv+71pRFN2SRPRuwcaYm0slxcr4k7McK7dY8lVW54sr7ZS8K6ucjzQf5eVZlN8s7dBEC6CCE7ar41w/3rsCRMbJqDxxt3C+PynDyoCtkkZ/RGtQfU6nyu6jIJXlzLQMJc2W9WBUmGX05YpAASgpQljMRaxhkUIQEAMgk+0MudlkEUyy6nkjZvROsVuVCdGd58W/GBJsRmibBqE4xubP0YWXY5pf92sxfZCxpek0Y+zR/5NBfrHndyKS6kbH6reyz968ra3VPsjtUCdHnOd0q28MPdgsPDXpPGVI4fp3KcD/I/T+X4Ncj/PpXjoidxJ7NcmMzypy6yHkvUGywtuSxutAihJWAavAkwM85XpQpHZ8Y5B4hShSMz43wf5A/NjPNDqcKBpuSbgsOOUoUnZsb5CRCBKUv7FQDFMlfVOAWHh7Q+okPeurHAgymbLMBo2phcZ+6AJkDBIa07HOQ7OlksU1VGCg47OpEAhE5eu+aRTrbVtaUKLdL5NLs2QgRDSvkLsA94igEEACiWqRYc8sUyd5TBgsN9OolimWudBg9E6EsAhd2OsXcQgr3Q2gOu3uZEqcKxUoUXgsGPKqdKlVaEJ0oVDgGUKhyIkgcolplbL+dSNtl8ml35NLt2O8be9SYP/gyQ16pcgvAm1C9cj2+jdQWH+wHr6m0ecz2+63RtwWHPtSp7pnL8BYgbCzw6meUSYFWX2AuQshm5xzYyg/rVLyzQntU93wxWw/VW1xXLpJI2+4QJwuSZZZdz0T6ZBM/dWAChvQLp8UHCIiUMrI0kD2D8syj/VoV2M6CfWGDZ5WutuFtlkjb7lLi1Ol+q+mwyHE1uZSBkVGpyVhVWHX/1uSRqdc53asskeByGNxS2QqFqsASUEMLEWljmtGrOJnlt0AHaEY+Gx8LEEia2MPy+m0UewAwG95OBLQz/uEqY2Dp5gGi5HyhbAUFl39JTQvjrXZjYCYv0+tHrDTMhSGlOtoS4fYf32l2wsMzpECkTeyzN/nZ9x9LsD2x9HCFuCxM7Ify1rsqWMBKbfTJkqqmn7oASottFrTupJSdD6K3SyXBQGNjlmv+KXa5xRpsJ0fHszZz2OkzlQECs5dhajDkZDk6McHhihMPqbk+M+O8XO3O8LQys61XegvDS2MrTYQNWPoFFnwKz8xyPXjAxwsl+DLfZ6a3Z+ZXzgoLDp8NwLN5yQP8OqAuhi7AzFz4g7QdqZwdflCu3OD4s5GGDPo2FytpMGOpPYzq27cfRKLbt53Ed2/oPEjr+r3+RiREjRowYMWLEiBEjRowY2xH/AQRzj/3HnYoxAAAAAElFTkSuQmCC";

    // petal_down.png - 向下指的花瓣
    private const string petal_down_b64 = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAFVklEQVR4nO2aS4gcVRSGv9tV1elH0vOqyZMZnFQQVJQsBMWVuhPBhUrc+AAxi+BCRI3xgYIIiQ8EH4mIEl8LdeFCNyJuXIlC3ATc2Zkww4QEe9LTHaan7e7qclF1e+7UVM9Mpm91LawfGs65fercc849955bp1sQAzzP89wubbdLu+Wy4nbpuJ7PT+5kJhD7FaBc4aN+evbs4pMr13hO8ocmxZe6bRW6FarOux6dgO4FQNIHRrgZoFzh4Q3U7QwP6A5CRqcy1fnNZOeqnAced2wKfUTWOQ/w9z/eE4PYGIa2AISd32j13a7/HWACZkbwkqqrlOOdzebSZbepS9E2YUjCMnjNyGAaAmuYBmjJgK2sfviZmQk+i6IlSjlej5prapQzbpe2rizQcgiGAzC/tHpyjxV4pReY0BaQjs8u8pSRwZIZYGT8zDQyWNUGr0pdU6OckeNGBksIMbD9AyuQzgO4Xdqq8xKlHMfdLh0AGYDpMT5VZeaqHFW3gJHBNDJYEbxpZHpjAwdhoC2wFecB6k3eBt95ZdiIoqVMvcmH1QbvVZZ5S9Ult5ecc9CtoLUMXg9mF3kM33EjoHtYbvGxyoeDoBPbDoAa+a3U/TBmJviGoAwG9KZQ51HpQbJAawbsH4leqVKO4xHDkVtg2BgoAGtWxPMPuT27eFOV6eM8s4scYXULHFG/K2Y5pvJ2kRflIarOFbZhO9j2CRo+AKVRsv4HhrbDJVDS02N8p+qbq/JIr/yFSmG4EgQyWqpBkofggwDlCjVJJ4HEAjAzwQ/lCjWArsfnSdkx9AA025xtu3xVrtBwbMYBHJvdw7ZDIvaXoXqTnxT2gvpdEITds4vcb2Sg2eZUzuJE3DapiDUDqg1+3IKYOTPBz22Xd8EPQpw2rZs8LsWVZb6/DnHDsbmlXOGvqAyoLHNW0vtHeFqLgQEGygBZhsAvS8p4v8AeVBnHZpLgEjRX5b4o56sNvlb5SzVOh+YaqH+QRBU46Njsc2z2EVyFAXN6jF/CgvUm30YpmF/ifV3GbDsA6sVjTSYEtF3koajnchZ3E731rms7qnMO8kocawaMFXhA5QPnmatyD8rqA2YwNnRob4jI9/Ur1zhpF3lBbYiCfz1utvlCPu/YTEt6oca94Wtv+AwAuGGc5wOZZBsi/XDlGicBKsvru7uq8wDlCnMBGbkF7CJPqvzUKM/osdJHLD1B16NzqcYbagYA1JtrGx0qLINHo3qCkpftsN64pp6glgwQQghp1PwSJwyBuX9ktc29lVI1PcYfKj8M50HzFrh41e8Jzi9t6zqbyG8U2gIghBCOzQcAU6Ocku/rahaUcmsbHRKOzU0LNe4IrXjsqw+aM0AGYaOUL2Y5qvKOza2AeWCEP/s9M7/E8TichxjSTgghek3KLrisb1k5NocBNzQc2Re8VONlgItXefbQpDit19qEGiILNQ4T9APlJxhbh6lR/zcFx+7/P4JBoP3/ARL9/iQxXuC3jZ672uAuuf+zBvm4Ul8itgzo967A6qqHsWZc111/M8SmWKLjei15RW51WGm5NNwunb0lzqtyl+vcFqx6IWuSl1XENEQ2TvtiD4DneV6rQ0MGYCTP7xvJ11a4UwYga1KIc/VhCJcP6UDH9VqhOTshURMga5LPGuTjXvmefcOYRKLjeq2Wy0qQCefU72or3D5s52HI10/TENl/294ywOU6N660qQPkLUp5y2+rDdN5SOD+vcMSRRmEUAB27bBEcdj2JHIRinI0CecTx4WKd26p4V1O0obEfhuUGC2IvUnbkCJFihQpUqRIkSJFihQpUqRIkSJFiv8P/gMdgXfDmw237wAAAABJRU5ErkJggg==";

    // petal_left.png - 向左指的花瓣
    private const string petal_left_b64 = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAFFUlEQVR4nO2aT2hcRRzHP7NvX9gkzW42bkwxzVJJPFhEEA/exIsH8SiIBwUVBbVqwVr/45/ioYhBRLSHQrEtgoJevHi0IAhCkaB43FYaG1L7mj+b7ma7770dD/smmcy+ze7W7GYh84XH/mbeb34zv+/M/ObNzIKFhYWFhYWFhYWFhYXF3oPY7Qa0Cyml7ERfCNGWb31PgOl4WMPfTt9J4OrpVkT0LQHK8VYOt4KTwN2OhL4kQEopTcc7JUIfCUqOI6KvCIjr9QYiJMF2NhxBcku6BRFblHcTUkpZ8Hj14BizWwho4bCJUBIsrPKmSk+NMhtXlyKhL0aAcl6lp0Y5YTre7hS4usZ7Zp5JgpPAVbEhcauN3gnICGENf2qUE7Dp/MIqx8MavnpUmbBG0OyJcx5gfoWjOqHKppRS7toI0APdxm/UyIVVjiu93DDHALwSP2h5j8XZ9Ep82Ky+iRE+VjFAxQkngbsrIyAIZVXv3VAShJJApZXTuWGOhRJfdx7qZIQS33y2qzMaJQ319XQEqF5XPV4NWTcbCaA7s1zmx2b20ikeMfOKFT4x87JDvAvgiGgEJDaDf09HQDWgrHqgGrLeMI9Vb8bM/XaRTvGGmdZ6XtnfqLNny2AQyqrq8WpQ/zWHrRnsWtlspjM8wGu6jurxDftat/eEAOW86bjZw8UKJ5U8PMDzACmXhyo+5zW1i0poNe+hPuy3I7PrMeCmL0uhJBh0+QUIrt3g3vF9/A6Ei0XuU3qlKqfMsimXp5UckXAxRufZTtukYsH+NHd1dQTc9GVp3WcNYDD6INV7I5T4kxnmCh5zceUrPl+7Dk/ms/wMBAWvUaedqdKARKy4s1gpy8V1n7V1n2JmkAtQb+j+NH8AIcBkhjklN4Nyfpv334c1/HyW76Lfb/JZzkby6XyWU5F8UguuwWSGQ8D5HZ8CFz15wcy78zYWNScCNp0OgLDg8Wcze9M5JvWyBY9r2ruhgsfSdI5MM9uarGyEkXyWbhAA9d5fKvOPSrciACCOhOkcea3BDWULHv9quplmtokhoOBRhi5NgdEhsX9siANjQxwYdEm3U2Y6x91GOt9E1QG4dJ1Hp3PcDpAQPBPlPa69f0qTnwOSl67zwqXrHFbOQ5dXAT0IqjiwWOSQigNXVrlHxYErq9w/meE3ILi8zAP5LL9G8oMqDlxe5uF8lp8KHn+5Dq/fSpv8cHOfAX20DEJ763rFr+8aAVIub3XanorP+3q6J3uBdj+E4pa0OFIqPifadV6t+QqlKm/r6Z5thrrxKRyHUnXL1+SLsHXzA1Cs1Heb0OMToZu+LEF936/WY/19s5HRLooVvjDz0ile0d5/pL26AT0+ExxIMhTW8KmB45CssnU7TC361dam5TLnlJxO8USndapDkOVyw2nRvokRZnu6HRZCiKQjBtSZ3IDD4NU1PlBpJ0HSSZB0BK4jcHXnAYoVvtV0G564OiM7sUdlV9c4uisnQoqE+ZX63n1hlXccQVInwitxOq7scplziiDzyQ1vngYDmOnYtuyMS51DCCGi0+CXD47xWVjD3zjT/x/dMjFSP/1pF7t6KiyEEDPj4ktzCJuXGyY2pkrMo+lsTI2pUb6KszMzLs70xb0AxJ8Sz6/wual3R4bDndjVyZxf4SUlz4yLM9AnFyMKcVdjfy/xqZKnRjmy01djfUWAwp69HNWxp6/HdezZP0iY6NZfZCwsLCwsLCwsLCwsLCz2Iv4DfJUaKAbWFG0AAAAASUVORK5CYII=";

    // HUD icon - 冰莲花小图标
    private const string hud_icon_b64 = "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";

    public void Tick(int petalCount)
    {
        Camera? hudCamera = GameCameras.instance != null ? GameCameras.instance.hudCamera : null;
        if (hudCamera == null || !hudCamera.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        EnsureBuilt();
        if (_root == null || _coreRenderer == null)
        {
            return;
        }

        Vector3 worldPosition = GetHudWorldPosition(hudCamera);
        _root.transform.position = worldPosition;
        _root.transform.rotation = Quaternion.identity;
        _root.transform.localScale = Vector3.one;

        bool hasShield = petalCount > 0;
        SetVisible(hasShield);
        if (!hasShield)
        {
            return;
        }

        float shieldFill = petalCount / (float)MaxPetals;
        _coreRenderer.color = new Color(0.84f, 0.96f, 1f, 0.32f + (0.44f * shieldFill));
        for (int i = 0; i < _petalRenderers.Length; i++)
        {
            SpriteRenderer renderer = _petalRenderers[i];
            int layerIndex = i / PetalsPerLayer;
            float layerFade = 1f - (layerIndex * 0.08f);
            Color color = i < petalCount ? ActivePetalColor : InactivePetalColor;
            renderer.color = new Color(color.r, color.g, color.b, color.a * layerFade);
        }
    }

    public void Dispose()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }

        _root = null;
        _coreRenderer = null;
        _petalRenderers = new SpriteRenderer[MaxPetals];
    }

    private void EnsureBuilt()
    {
        if (_root != null)
        {
            return;
        }

        _root = new GameObject("DeVect_IceShieldDisplay");
        ApplyHudLayer(_root);

        // 创建核心发光球
        GameObject core = new("Core");
        core.transform.SetParent(_root.transform, false);
        ApplyHudLayer(core);
        _coreRenderer = core.AddComponent<SpriteRenderer>();
        _coreRenderer.sprite = CreateCoreSprite();
        _coreRenderer.sortingLayerName = "HUD";
        _coreRenderer.sortingOrder = 18;
        _coreRenderer.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

        // 创建 4 层护盾 HUD，每层 4 瓣
        for (int layerIndex = 0; layerIndex < LayerCount; layerIndex++)
        {
            float layerScale = 0.18f + (0.05f * layerIndex);
            float layerOffsetScale = 0.72f + (0.18f * layerIndex);
            for (int direction = 0; direction < PetalsPerLayer; direction++)
            {
                int petalIndex = (layerIndex * PetalsPerLayer) + direction;
                GameObject petal = new($"Petal_{layerIndex}_{direction}");
                petal.transform.SetParent(_root.transform, false);
                ApplyHudLayer(petal);
                petal.transform.localPosition = PetalOffsets[direction] * layerOffsetScale;
                petal.transform.localScale = new Vector3(layerScale, layerScale, 1f);
                petal.transform.localRotation = Quaternion.identity;

                SpriteRenderer renderer = petal.AddComponent<SpriteRenderer>();
                renderer.sprite = GetPetalSprite(direction);
                renderer.sortingLayerName = "HUD";
                renderer.sortingOrder = 19 + layerIndex;
                _petalRenderers[petalIndex] = renderer;
            }
        }
    }

    private static void ApplyHudLayer(GameObject obj)
    {
        obj.layer = HudLayer;
    }

    private static Vector3 GetHudWorldPosition(Camera hudCamera)
    {
        if (TryGetMaskAnchorWorldPosition(hudCamera, out Vector3 anchorWorldPosition))
        {
            return anchorWorldPosition;
        }

        PlayerData? playerData = PlayerData.instance;
        int maxHealth = Mathf.Max(5, playerData?.maxHealth ?? 5);
        int blueHealth = Mathf.Max(0, playerData?.healthBlue ?? 0);
        float viewportX = HealthHudStartViewportX + ((maxHealth + blueHealth) * HealthHudUnitViewportSpacing);
        float worldDistance = Mathf.Abs(hudCamera.transform.position.z);
        Vector3 worldPosition = hudCamera.ViewportToWorldPoint(new Vector3(viewportX, HudViewportY, worldDistance));
        worldPosition.z = 0f;
        return worldPosition;
    }

    private static bool TryGetMaskAnchorWorldPosition(Camera hudCamera, out Vector3 worldPosition)
    {
        worldPosition = default;

        Transform? hudRoot = hudCamera.transform.parent != null ? hudCamera.transform.parent : hudCamera.transform;
        if (TryGetRightmostHudSpriteBounds(hudCamera, hudRoot, requireHealthKeyword: true, out Bounds maskBounds) ||
            TryGetRightmostHudSpriteBounds(hudCamera, hudRoot, requireHealthKeyword: false, out maskBounds))
        {
            worldPosition = new Vector3(
                maskBounds.max.x + MaskAnchorWorldOffsetX,
                maskBounds.center.y + MaskAnchorWorldOffsetY,
                0f);
            return true;
        }

        return false;
    }

    private static bool TryGetRightmostHudSpriteBounds(Camera hudCamera, Transform root, bool requireHealthKeyword, out Bounds bestBounds)
    {
        bestBounds = default;
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        float bestRightEdge = float.NegativeInfinity;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (!IsEligibleHudSpriteRenderer(hudCamera, renderer, requireHealthKeyword))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (!found || bounds.max.x > bestRightEdge)
            {
                bestBounds = bounds;
                bestRightEdge = bounds.max.x;
                found = true;
            }
        }

        return found;
    }

    private static bool IsEligibleHudSpriteRenderer(Camera hudCamera, SpriteRenderer renderer, bool requireHealthKeyword)
    {
        if (renderer == null ||
            renderer.sprite == null ||
            !renderer.gameObject.activeInHierarchy ||
            renderer.sortingLayerName != "HUD" ||
            renderer.gameObject.layer != HudLayer)
        {
            return false;
        }

        string objectName = renderer.gameObject.name;
        if (objectName.StartsWith("DeVect_"))
        {
            return false;
        }

        string objectNameLower = objectName.ToLowerInvariant();
        bool matchesHealthKeyword = false;
        for (int i = 0; i < HealthNameKeywords.Length; i++)
        {
            if (objectNameLower.Contains(HealthNameKeywords[i]))
            {
                matchesHealthKeyword = true;
                break;
            }
        }

        if (requireHealthKeyword && !matchesHealthKeyword)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        Vector3 viewport = hudCamera.WorldToViewportPoint(bounds.center);
        if (viewport.z <= 0f || viewport.y < 0.78f || viewport.y > 1.05f || viewport.x < -0.05f || viewport.x > 0.45f)
        {
            return false;
        }

        Vector3 size = bounds.size;
        if (size.x <= 0f || size.y <= 0f || size.x > 2.5f || size.y > 2.5f)
        {
            return false;
        }

        return true;
    }

    private void SetVisible(bool visible)
    {
        if (_root != null && _root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    /// <summary>
    /// 获取指定方向的花瓣sprite
    /// 0=上, 1=右, 2=下, 3=左
    /// </summary>
    private static Sprite GetPetalSprite(int direction)
    {
        if (_petalSprites[direction] != null)
        {
            return _petalSprites[direction]!;
        }

        string b64 = direction switch
        {
            0 => petal_up_b64,
            1 => petal_right_b64,
            2 => petal_down_b64,
            3 => petal_left_b64,
            _ => petal_up_b64
        };

        Texture2D texture = DecodeBase64Texture(b64);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = $"DeVect_IceShieldPetal_{direction}";

        string[] names = { "Up", "Right", "Down", "Left" };
        _petalSprites[direction] = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width
        );
        _petalSprites[direction]!.name = $"DeVect_IceShieldPetal_{names[direction]}Sprite";
        return _petalSprites[direction]!;
    }

    private static Sprite CreateCoreSprite()
    {
        if (_coreSprite != null)
        {
            return _coreSprite;
        }

        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "DeVect_IceShieldCore"
        };

        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                if (distance > 1f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float alpha = Mathf.Pow(1f - distance, 1.25f);
                // 冰蓝色发光核心
                texture.SetPixel(x, y, new Color(0.72f + (alpha * 0.23f), 0.88f + (alpha * 0.1f), 1f, alpha));
            }
        }

        texture.Apply();
        _coreSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _coreSprite.name = "DeVect_IceShieldCoreSprite";
        return _coreSprite;
    }

    /// <summary>
    /// 解码Base64字符串为Texture2D
    /// </summary>
    private static Texture2D DecodeBase64Texture(string b64)
    {
        byte[] bytes = System.Convert.FromBase64String(b64);
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.LoadImage(bytes);
        return texture;
    }

    /// <summary>
    /// 获取HUD图标sprite（如果需要的话）
    /// </summary>
    public static Sprite GetHudIconSprite()
    {
        if (_hudIconSprite != null)
        {
            return _hudIconSprite;
        }

        Texture2D texture = DecodeBase64Texture(hud_icon_b64);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = "DeVect_IceShieldHudIcon";

        _hudIconSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width
        );
        _hudIconSprite.name = "DeVect_IceShieldHudIconSprite";
        return _hudIconSprite;
    }
}
