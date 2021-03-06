//
//  ManifestHandler.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2016 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using Launchpad.Launcher.Utility;

namespace Launchpad.Launcher.Handlers
{
	internal sealed class ManifestHandler
	{
		private readonly object GameManifestLock = new object();
		private readonly object OldGameManifestLock = new object();

		private readonly object LaunchpadManifestLock = new object();
		private readonly object OldLaunchpadManifestLock = new object();

		/// <summary>
		/// The config handler reference.
		/// </summary>
		ConfigHandler Config = ConfigHandler.Instance;

		private List<ManifestEntry> gameManifest = new List<ManifestEntry>();

		/// <summary>
		/// Initializes a new instance of the <see cref="Launchpad.Launcher.Handlers.ManifestHandler"/> class.
		/// This constructor also serves to updated outdated file paths for the manifests.
		/// </summary>
		public ManifestHandler()
		{
			ReplaceDeprecatedManifest();
		}

		/// <summary>
		/// Gets the manifest. Call sparsely, as it loads the entire manifest from disk each time 
		/// this property is accessed.
		/// </summary>
		/// <value>The manifest.</value>
		public List<ManifestEntry> GameManifest
		{
			get
			{
				LoadGameManifest();
				return gameManifest;
			}
		}

		private readonly List<ManifestEntry> oldGameManifest = new List<ManifestEntry>();

		/// <summary>
		/// Gets the old manifest. Call sparsely, as it loads the entire manifest from disk each time
		/// this property is accessed.
		/// </summary>
		/// <value>The old manifest.</value>
		public List<ManifestEntry> OldGameManifest
		{
			get
			{
				LoadOldGameManifest();
				return oldGameManifest;
			}
		}

		private readonly List<ManifestEntry> launchpadManifest = new List<ManifestEntry>();

		public List<ManifestEntry> LaunchpadManifest
		{
			get
			{
				LoadLaunchpadManifest();
				return launchpadManifest;
			}
		}

		private readonly List<ManifestEntry> oldLaunchpadManifest = new List<ManifestEntry>();

		public List<ManifestEntry> OldLaunchpadManifest
		{
			get
			{
				LoadOldLaunchpadManifest();
				return oldLaunchpadManifest;
			}
		}

		/// <summary>
		/// Loads the game manifest from disk.
		/// </summary>
		private void LoadGameManifest()
		{
			try
			{
				lock (GameManifestLock)
				{
					if (File.Exists(GetGameManifestPath()))
					{
						gameManifest.Clear();

						string[] rawGameManifest = File.ReadAllLines(GetGameManifestPath());
						foreach (string rawEntry in rawGameManifest)
						{
							ManifestEntry newEntry = new ManifestEntry();
							if (ManifestEntry.TryParse(rawEntry, out newEntry))
							{
								gameManifest.Add(newEntry);
							}
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in LoadGameManifest(): " + ioex.Message);
			}
		}

		/// <summary>
		/// Loads the old game manifest from disk.
		/// </summary>
		private void LoadOldGameManifest()
		{
			try
			{
				lock (OldGameManifestLock)
				{
					if (File.Exists(GetOldGameManifestPath()))
					{
						oldGameManifest.Clear();

						string[] rawOldGameManifest = File.ReadAllLines(GetOldGameManifestPath());
						foreach (string rawEntry in rawOldGameManifest)
						{
							ManifestEntry newEntry = new ManifestEntry();
							if (ManifestEntry.TryParse(rawEntry, out newEntry))
							{
								oldGameManifest.Add(newEntry);
							}
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in LoadOldGameManifest(): " + ioex.Message);
			}
		}


		/// <summary>
		/// Loads the launchpad manifest from disk.
		/// </summary>
		private void LoadLaunchpadManifest()
		{
			try
			{
				lock (LaunchpadManifestLock)
				{
					if (File.Exists(GetLaunchpadManifestPath()))
					{
						launchpadManifest.Clear();

						string[] rawLaunchpadManifest = File.ReadAllLines(GetLaunchpadManifestPath());
						foreach (string rawEntry in rawLaunchpadManifest)
						{
							ManifestEntry newEntry = new ManifestEntry();
							if (ManifestEntry.TryParse(rawEntry, out newEntry))
							{
								launchpadManifest.Add(newEntry);
							}
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in LoadLaunchpadManifest(): " + ioex.Message);
			}
		}

		/// <summary>
		/// Loads the old launchpad manifest from disk.
		/// </summary>
		private void LoadOldLaunchpadManifest()
		{
			try
			{
				lock (OldLaunchpadManifestLock)
				{
					if (File.Exists(GetOldGameManifestPath()))
					{
						oldLaunchpadManifest.Clear();

						string[] rawOldLaunchpadManifest = File.ReadAllLines(GetOldGameManifestPath());
						foreach (string rawEntry in rawOldLaunchpadManifest)
						{
							ManifestEntry newEntry = new ManifestEntry();
							if (ManifestEntry.TryParse(rawEntry, out newEntry))
							{
								oldLaunchpadManifest.Add(newEntry);
							}
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in LoadOldLaunchpadManifest(): " + ioex.Message);
			}
		}

		/// <summary>
		/// Gets the game manifests' path on disk.
		/// </summary>
		/// <returns>The game manifest path.</returns>
		public static string GetGameManifestPath()
		{
			string manifestPath = String.Format(@"{0}GameManifest.txt", 
				                      ConfigHandler.GetLocalDir());
			return manifestPath;
		}

		/// <summary>
		/// Gets the old game manifests' path on disk.
		/// </summary>
		/// <returns>The old game manifest's path.</returns>
		public static string GetOldGameManifestPath()
		{
			string oldManifestPath = String.Format(@"{0}GameManifest.txt.old", 
				                         ConfigHandler.GetLocalDir());
			return oldManifestPath;
		}

		/// <summary>
		/// Gets the launchpad manifests' path on disk.
		/// </summary>
		/// <returns>The launchpad manifest path.</returns>
		public static string GetLaunchpadManifestPath()
		{
			string manifestPath = String.Format(@"{0}LaunchpadManifest.txt", 
				                      ConfigHandler.GetLocalDir());
			return manifestPath;
		}

		/// <summary>
		/// Gets the old launchpad manifests' path on disk.
		/// </summary>
		/// <returns>The old launchpad manifest's path.</returns>
		public static string GetOldLaunchpadManifestPath()
		{
			string oldManifestPath = String.Format(@"{0}LaunchpadManifest.txt.old", 
				                         ConfigHandler.GetLocalDir());
			return oldManifestPath;
		}

		/// <summary>
		/// Gets the deprecated manifests' path on disk.
		/// </summary>
		/// <returns>The deprecated manifest path.</returns>
		private static string GetDeprecatedGameManifestPath()
		{
			string manifestPath = String.Format(@"{0}LauncherManifest.txt", 
				                      ConfigHandler.GetLocalDir());
			return manifestPath;
		}

		/// <summary>
		/// Gets the deprecated old manifests' path on disk.
		/// </summary>
		/// <returns>The deprecated old manifest's path.</returns>
		private static string GetDeprecatedOldGameManifestPath()
		{
			string oldManifestPath = String.Format(@"{0}LauncherManifest.txt.old", 
				                         ConfigHandler.GetLocalDir());
			return oldManifestPath;
		}

		/// <summary>
		/// Gets the game manifest URL.
		/// </summary>
		/// <returns>The game manifest URL.</returns>
		public string GetGameManifestURL()
		{
			string manifestURL = String.Format("{0}/game/{1}/GameManifest.txt", 
				                     Config.GetBaseProtocolURL(),
				                     Config.GetSystemTarget());

			return manifestURL;
		}

		/// <summary>
		/// Gets the game manifest checksum URL.
		/// </summary>
		/// <returns>The game manifest checksum URL.</returns>
		public string GetGameManifestChecksumURL()
		{
			string manifestChecksumURL = String.Format("{0}/game/{1}/GameManifest.checksum", 
				                             Config.GetBaseProtocolURL(), 
				                             Config.GetSystemTarget());

			return manifestChecksumURL;
		}

		/// <summary>
		/// Gets the launchpad manifest URL.
		/// </summary>
		/// <returns>The launchpad manifest URL.</returns>
		public string GetLaunchpadManifestURL()
		{
			string manifestURL = String.Format("{0}/launcher/LaunchpadManifest.txt", 
				                     Config.GetBaseProtocolURL());

			return manifestURL;
		}

		/// <summary>
		/// Gets the launchpad manifest checksum URL.
		/// </summary>
		/// <returns>The launchpad manifest checksum URL.</returns>
		public string GetLaunchpadManifestChecksumURL()
		{
			string manifestChecksumURL = String.Format("{0}/launcher/LaunchpadManifest.checksum", 
				                             Config.GetBaseProtocolURL());

			return manifestChecksumURL;
		}

		/// <summary>
		/// Replaces the deprecated manifest, moving LauncherManifest to GameManifest (if present).
		/// This function should only be called once per launcher start.
		/// </summary>
		public void ReplaceDeprecatedManifest()
		{
			if (File.Exists(GetDeprecatedGameManifestPath()))
			{
				lock (GameManifestLock)
				{
					File.Move(GetDeprecatedGameManifestPath(), GetGameManifestPath());
				}
			}

			if (File.Exists(GetDeprecatedOldGameManifestPath()))
			{
				lock (OldGameManifestLock)
				{
					File.Move(GetDeprecatedOldGameManifestPath(), GetOldGameManifestPath());
				}
			}
		}
	}

	/// <summary>
	/// A manifest entry derived from the raw unformatted string.
	/// Contains the relative path of the referenced file, as well as
	/// its MD5 hash and size in bytes.
	/// </summary>
	internal sealed class ManifestEntry : IEquatable<ManifestEntry>
	{
		public string RelativePath
		{
			get;
			set;
		}

		public string Hash
		{
			get;
			set;
		}

		public long Size
		{
			get;
			set;
		}

		public ManifestEntry()
		{
			RelativePath = String.Empty;
			Hash = String.Empty;
			Size = 0;
		}

		/// <summary>
		/// Attempts to parse an entry from a raw input.
		/// The input is expected to be in [path]:[hash]:[size] format.
		/// </summary>
		/// <returns><c>true</c>, if the input was successfully parse, <c>false</c> otherwise.</returns>
		/// <param name="rawInput">Raw input.</param>
		/// <param name="inEntry">The resulting entry.</param>
		public static bool TryParse(string rawInput, out ManifestEntry inEntry)
		{
			//clear out the entry for the new data
			inEntry = new ManifestEntry();

			if (!String.IsNullOrEmpty(rawInput))
			{
				//remove any and all bad characters from the input string, 
				//such as \0, \n and \r.
				string cleanInput = Utilities.Clean(rawInput);

				//split the string into its three components - file, hash and size
				string[] entryElements = cleanInput.Split(':');

				//if we have three elements (which we should always have), set them in the provided entry
				if (entryElements.Length == 3)
				{
					//clean the manifest path, converting \ to / on unix and / to \ on Windows.
					if (ChecksHandler.IsRunningOnUnix())
					{
						inEntry.RelativePath = entryElements[0].Replace("\\", "/");
					}
					else
					{
						inEntry.RelativePath = entryElements[0].Replace("/", "\\");
					}

					//set the hash to the second element
					inEntry.Hash = entryElements[1];

					//attempt to parse the final element as a long-type byte count.
					long parsedSize;
					if (long.TryParse(entryElements[2], out parsedSize))
					{
						inEntry.Size = parsedSize;
						return true;
					}
					else
					{
						//could not parse the size, parsing has failed.
						return false;
					}
				}
				else
				{
					//wrong number of raw entry elements, parsing has failed.
					return false;
				}
			}
			else
			{
				//no input, parsing has failed
				return false;
			}
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/>.
		/// The returned value matches a raw in-manifest representation of the entry, in the form of
		/// [path]:[hash]:[size]
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/>.</returns>
		public override string ToString()
		{
			return RelativePath + ":" + Hash + ":" + Size;
		}

		/// <summary>
		/// Determines whether the specified <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/> is equal to the current <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/>.
		/// </summary>
		/// <param name="Other">The <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/> to compare with the current <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/> is equal to the current
		/// <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/>; otherwise, <c>false</c>.</returns>
		public bool Equals(ManifestEntry Other)
		{
			return this.RelativePath == Other.RelativePath &&
			this.Hash == Other.Hash &&
			this.Size == Other.Size;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Launchpad.Launcher.Handlers.ManifestEntry"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public int GetHashCode()
		{
			return this.ToString().GetHashCode();
		}

		/// <summary>
		/// Verifies the integrity of the file in the manifest entry.
		/// </summary>
		/// <returns><c>true</c>, if file was complete and undamaged, <c>false</c> otherwise.</returns>
		public bool IsFileIntegrityIntact()
		{
			string LocalPath = String.Format("{0}{1}", 
				                   ConfigHandler.Instance.GetGamePath(),
				                   RelativePath);

			if (!File.Exists(LocalPath))
			{
				return false;
			}
			else
			{
				FileInfo fileInfo = new FileInfo(LocalPath);
				if (fileInfo.Length != Size)
				{
					return false;
				}
				else
				{
					using (Stream file = File.OpenRead(LocalPath))
					{
						string localHash = MD5Handler.GetStreamHash(file);
						if (localHash != Hash)
						{
							return false;
						}
					}
				}
			}

			return true;
		}
	}
}

