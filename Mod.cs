using System.Text.Json.Serialization;
using Semver;

namespace Olib.Modding
{
    /// <summary>
    /// Represents a mod with its metadata and dependencies. Each mod has a name, version, author, description, website, and a list of dependencies on other mods. The dependencies specify which other mods are required for this mod to function correctly, along with the compatible version ranges for those dependencies. This class serves as the core data structure for representing mods in the modding system, allowing the ModManager to manage and validate mods based on their metadata and dependencies.
    /// </summary>
    public class Mod
    {
        public string Name { get; set; } = string.Empty;
        public SemVersion Version { get; set; } = new SemVersion(0, 0, 0);
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public List<Dependency> Dependencies { get; set; } = new List<Dependency>();

        [JsonIgnore]
        public string DirectoryPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a dependency on another mod, including the name of the required mod and the compatible version range. This class is used within the Mod class to specify which other mods are required for a mod to function correctly. The IsCompatible method can be used to check if a given mod satisfies this dependency by comparing the mod's name and version against the specified requirements. This allows the ModManager to validate active mods and ensure that all dependencies are met before activation.
    /// </summary>
    public class Dependency
    {
        public required string Name { get; set; }
        public required SemVersionRange CompatibleVersions { get; set; }

        public bool IsCompatible(Mod mod)
        {
            return mod.Name == Name && CompatibleVersions.Contains(mod.Version);
        }
    }

    /// <summary>
    /// Represents an active mod that is intended to be used in the game. It contains the name and version of the mod, which can be used to identify it and check against the list of available mods for validation. This class is used in the ModManager to manage the list of mods that are currently active and to validate their dependencies before activation. It serves as a simplified representation of a mod that focuses on its identity (name and version) rather than its full metadata and dependencies, which are contained in the Mod class.
    /// </summary>
    public class ActiveMod
    {
        public string Name { get; private set; } = string.Empty;
        public SemVersion Version { get; private set; } = new SemVersion(0, 0, 0);
    }
}
