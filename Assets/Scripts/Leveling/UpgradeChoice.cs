using System;

namespace ArenaSurvivor.Leveling
{
    /// <summary>
    /// Qué clase de mejora es — lo único que le importa a la ventanita de "instalando/
    /// actualizando" para decidir su texto y color; el resto de la mejora no le interesa.
    /// </summary>
    public enum UpgradeChoiceKind
    {
        NewWeapon,
        WeaponUpgrade,
        StatBoost
    }

    /// <summary>Una opción de mejora concreta: un título para mostrar y qué hacer al elegirla.</summary>
    public class UpgradeChoice
    {
        public string Title { get; }
        public string Name { get; }
        public UpgradeChoiceKind Kind { get; }
        private readonly Action _apply;

        public UpgradeChoice(string title, string name, UpgradeChoiceKind kind, Action apply)
        {
            Title = title;
            Name = name;
            Kind = kind;
            _apply = apply;
        }

        public void Apply() => _apply?.Invoke();
    }
}
