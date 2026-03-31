using UnityEngine;

namespace DeVect.Combat;

internal sealed class IceShieldState
{
    public const int PetalsPerShield = 4;
    public const int MaxPetals = 4;

    private int _petalCount;

    public int GetPetalCount()
    {
        return _petalCount;
    }

    public int AddShield(int petals)
    {
        int previous = _petalCount;
        _petalCount = Mathf.Clamp(_petalCount + Mathf.Max(0, petals), 0, MaxPetals);
        return _petalCount - previous;
    }

    public int AbsorbDamage(int damageAmount, out int absorbedDamage)
    {
        int incomingDamage = Mathf.Max(0, damageAmount);
        absorbedDamage = Mathf.Min(incomingDamage, _petalCount / PetalsPerShield);
        if (absorbedDamage > 0)
        {
            _petalCount = Mathf.Max(0, _petalCount - (absorbedDamage * PetalsPerShield));
        }

        return Mathf.Max(0, incomingDamage - absorbedDamage);
    }

    public void SetPetalCount(int petals)
    {
        _petalCount = Mathf.Clamp(petals, 0, MaxPetals);
    }

    public void Clear()
    {
        _petalCount = 0;
    }
}
