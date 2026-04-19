using System.Text.Json;
using Godot;
using Semver;

namespace Olib.Modding
{
    /// <summary>
    /// Manages mod discovery, validation, and activation. It can search specified directories for mods, validate their dependencies, and activate them for use in the game.
    /// </summary>
    public class ModManager
    {
        /// <summary>
        /// List of directories to search for mods. Each directory should contain subdirectories for individual mods, each with a mod.json file defining the mod's metadata and dependencies.
        /// </summary>
        public List<string> ModDirectories { get; private set; }

        /// <summary>
        /// List of mods that have been discovered in the specified directories. This list is populated by the FindMods method and contains all mods that are available for activation.
        /// </summary>
        public List<Mod> AvailableMods { get; private set; } = new List<Mod>();

        /// <summary>
        /// List of mods that have been activated for use in the game. This list is populated by the ActivateMods method and should only contain mods that have passed validation checks for dependencies.
        /// </summary>
        public List<Mod> ActiveMods { get; private set; } = new List<Mod>();

        /// <summary>
        /// If true, the ModManager will skip directories that are specified in ModDirectories but do not exist. If false, it will throw an exception if any of the specified directories cannot be accessed. This allows for more flexible mod directory configurations, especially during development or when optional mod directories are used.
        /// </summary>
        public bool SkipMissingDirectories { get; set; } = false;

        public ModManager(List<string> modDirectories, bool skipMissingDirectories = false)
        {
            ModDirectories = modDirectories;
            SkipMissingDirectories = skipMissingDirectories;
        }

        /// <summary>
        /// Searches the specified ModDirectories for mods by looking for subdirectories that contain a mod.json file. Each mod.json file is expected to contain the metadata and dependencies for a mod, which are deserialized into Mod objects and added to the AvailableMods list. If SkipMissingDirectories is false, an exception will be thrown if any of the specified directories cannot be accessed. If a mod.json file cannot be found or parsed in a subdirectory, a warning will be printed to the console, but the search will continue for other subdirectories.
        /// </summary>
        /// <param name="jsonOptions">Options to control the behavior of the JSON serializer.</param>
        /// <exception cref="ModException"></exception>
        public void FindMods(JsonSerializerOptions? jsonOptions = null)
        {
            foreach (var dir in ModDirectories)
            {
                Godot.DirAccess directory = Godot.DirAccess.Open(dir);
                if (directory == null)
                {
                    if (SkipMissingDirectories)
                    {
                        GD.PrintErr($"Warning: Mod directory not found: {dir}");
                        continue;
                    }
                    throw new ModException($"Could not access mod directory: {dir}");
                }

                foreach (var subdir in directory.GetDirectories())
                {
                    Godot.FileAccess file = Godot.FileAccess.Open($"{dir}/{subdir}/mod.json", Godot.FileAccess.ModeFlags.Read);
                    if (file == null)
                    {
                        GD.PrintErr($"Warning: mod.json not found in {dir}/{subdir}");
                        continue;
                    }
                    else
                    {
                        string jsonContent = file.GetAsText();
                        Mod? mod = (Mod?)JsonSerializer.Deserialize(jsonContent, typeof(Mod), jsonOptions);
                        if (mod == null)
                        {
                            throw new ModException($"Failed to parse mod definition in {dir}/{subdir}/mod.json");
                        }
                        mod.DirectoryPath = $"{dir}/{subdir}/";
                        AvailableMods.Add(mod);
                        file.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Validates the list of active mods by checking if each mod is present in the AvailableMods list and if all of its dependencies are satisfied. The method returns a list of ModDependencyReport objects that detail any missing, inactive, or incompatible dependencies for each active mod. If an active mod is not found in the AvailableMods list, it is reported as missing. For each dependency of an active mod, the method checks if it is present in the ActiveMods list (active), if it is present in the AvailableMods list but not active (inactive), or if it is present but does not satisfy the version requirements (incompatible). This validation step is crucial to ensure that all mods can function correctly without missing or conflicting dependencies before they are activated for use in the game.
        /// </summary>
        /// <param name="activeMods">The list of mods that are intended to be active.</param>
        /// <returns>A list of ModDependencyReport objects detailing the status of each active mod's dependencies.</returns>
        public List<ModDependencyReport> ValidateActiveMods(List<ActiveMod> activeMods)
        {
            List<ModDependencyReport> reports = new List<ModDependencyReport>();
            foreach (var activeMod in activeMods)
            {
                // is mod present
                var mod = AvailableMods.FirstOrDefault(m => m.Name == activeMod.Name && m.Version == activeMod.Version);
                if (mod == null)
                {
                    GD.PrintErr($"Error: Active mod not found: {activeMod.Name} v{activeMod.Version}");
                    reports.Add(new ModDependencyReport(new Mod { Name = activeMod.Name, Version = activeMod.Version }, true, new List<Dependency>(), new List<Dependency>(), new List<Dependency>()));
                    continue;
                }

                // check dependencies
                List<Dependency> missingDependencies = new List<Dependency>(); // not present at all
                List<Dependency> inactiveDependencies = new List<Dependency>(); // present but not active
                List<Dependency> incompatibleDependencies = new List<Dependency>(); // active but wrong version
                bool hasMissingOrIncompatible = false;
                foreach (var dependency in mod.Dependencies)
                {
                    var depMod = ActiveMods.FirstOrDefault(m => m.Name == dependency.Name);
                    if (depMod == null)
                    {
                        depMod = AvailableMods.FirstOrDefault(m => m.Name == dependency.Name);
                        if (depMod != null)
                        {
                            if (!dependency.IsCompatible(depMod))
                            {
                                missingDependencies.Add(dependency);
                                hasMissingOrIncompatible = true;
                                continue;
                            }
                            else
                            {
                                inactiveDependencies.Add(dependency);
                                hasMissingOrIncompatible = true;
                                continue;
                            }
                        }
                    }
                    else
                    {
                        if (!dependency.IsCompatible(depMod))
                        {
                            incompatibleDependencies.Add(dependency);
                            hasMissingOrIncompatible = true;
                        }
                    }
                }
                if (hasMissingOrIncompatible)
                {
                    reports.Add(new ModDependencyReport(mod, false, missingDependencies, inactiveDependencies, incompatibleDependencies));
                }
            }
            return reports;
        }

        /// <summary>
        /// Activates the specified list of active mods by adding them to the ActiveMods list. Before activation, it is recommended to call the ValidateActiveMods method to ensure that all mods in the activeMods list have their dependencies satisfied. If a mod in the activeMods list is not found in the AvailableMods list, an error message will be printed to the console, and that mod will not be activated. Successfully activated mods will have a confirmation message printed to the console. This method does not perform any dependency checks itself, so it assumes that the provided list of active mods has already been validated for missing or incompatible dependencies.
        /// </summary>
        /// <param name="activeMods">The list of mods to be activated.</param>
        public void ActivateMods(List<ActiveMod> activeMods)
        {
            foreach (var activeMod in activeMods)
            {
                var mod = AvailableMods.FirstOrDefault(m => m.Name == activeMod.Name && m.Version == activeMod.Version);
                if (mod != null)
                {
                    ActiveMods.Add(mod);
                    GD.Print($"Activated mod: {mod.Name} v{mod.Version}");
                }
                else
                {
                    GD.PrintErr($"Error: Could not activate mod (not found): {activeMod.Name} v{activeMod.Version}");
                }
            }
        }

        /// <summary>
        /// Creates a new mod definition and saves it as a mod.json file in the specified output directory. The method takes the mod's name, version, author, description, website, and dependencies as parameters to construct a Mod object. It then serializes this object to JSON format and writes it to a mod.json file in the output directory. If the output directory does not exist, it will be created. If there are any issues with creating the directory or writing the file, a ModException will be thrown with an appropriate error message. This method is useful for developers who want to create new mods by defining their metadata and dependencies programmatically and saving them in the correct format for discovery by the ModManager.
        /// </summary>
        /// <param name="name">The name of the mod.</param>
        /// <param name="version">The version of the mod.</param>
        /// <param name="author">The author of the mod.</param>
        /// <param name="description">A brief description of the mod.</param>
        /// <param name="website">The website associated with the mod.</param>
        /// <param name="dependencies">A list of dependencies required by the mod.</param>
        /// <param name="outputDirectory">The directory where the mod.json file will be saved.</param>
        /// <exception cref="ModException"></exception>
        public void CreateMod(string name, SemVersion version, string author, string description, string website, List<Dependency> dependencies, string outputDirectory)
        {
            Mod newMod = new Mod
            {
                Name = name,
                Version = version,
                Author = author,
                Description = description,
                Website = website,
                Dependencies = dependencies
            };

            Error error = DirAccess.MakeDirAbsolute(outputDirectory);
            if (error != Error.Ok)
            {
                throw new ModException($"Could not create output directory: {outputDirectory}. Error: {error}");
            }
            string jsonContent = JsonSerializer.Serialize(newMod, new JsonSerializerOptions { WriteIndented = true });
            Godot.FileAccess file = Godot.FileAccess.Open($"{outputDirectory}/mod.json", Godot.FileAccess.ModeFlags.Write);
            if (file == null)
            {
                throw new ModException($"Could not create mod file at: {outputDirectory}/mod.json");
            }
            file.StoreString(jsonContent);
            file.Close();
        }
    }

    public class ModException : Exception
    {
        public ModException(string message) : base(message) { }
    }

    /// <summary>
    /// Represents the result of validating a mod's dependencies. It contains the mod being validated, a flag indicating if the mod itself is missing, and lists of any missing, inactive, or incompatible dependencies. This report can be used to inform the user about issues with their active mods and what needs to be resolved before they can be successfully activated in the game.
    /// </summary>
    public class ModDependencyReport
    {
        public Mod Mod { get; set; }
        public bool IsMissing { get; set; }
        public List<Dependency> MissingDependencies { get; private set; } = new List<Dependency>();
        public List<Dependency> InactiveDependencies { get; private set; } = new List<Dependency>();
        public List<Dependency> IncompatibleDependencies { get; private set; } = new List<Dependency>();

        public ModDependencyReport(Mod mod, bool isMissing, List<Dependency> missingDependencies, List<Dependency> inactiveDependencies, List<Dependency> incompatibleDependencies)
        {
            Mod = mod;
            IsMissing = isMissing;
            MissingDependencies = missingDependencies;
            InactiveDependencies = inactiveDependencies;
            IncompatibleDependencies = incompatibleDependencies;
        }
    }
}
