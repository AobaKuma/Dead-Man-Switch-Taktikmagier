using Verse;

namespace Militarmagier
{
    /// <summary>
    /// Marks a psycast that must not get a vanilla psytrainer, such as the broom's flight, which only
    /// makes sense through the weapon that grants it. See <see cref="Patch_ThingDefGenerator_Neurotrainer_ImpliedThingDefs"/>.
    /// </summary>
    public class NoPsytrainerExtension : DefModExtension
    {
    }
}
