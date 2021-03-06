﻿//
//  FTPHandler.cs
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
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Generic;
using Launchpad.Launcher.Utility;
using Launchpad.Launcher.Utility.Enums;

namespace Launchpad.Launcher.Handlers.Protocols
{
	/// <summary>
	/// FTP handler. Handles downloading and reading files on a remote FTP server.
	/// There are also functions for retrieving remote version information of the game and the launcher.
	/// 
	/// This protocol uses a manifest.
	/// </summary>
	internal sealed class FTPProtocolHandler : PatchProtocolHandler
	{
		private readonly ManifestHandler manifestHandler = new ManifestHandler();

		/// <summary>
		/// Initializes a new instance of the <see cref="Launchpad.Launcher.Handlers.Protocols.FTPProtocolHandler"/> class. 
		/// This also calls the base PatchProtocolHandler constructor, setting up the common functionality.
		/// </summary>
		public FTPProtocolHandler()
			: base()
		{

		}

		public override bool CanPatch()
		{
			bool bCanConnectToFTP;

			string FTPURL = Config.GetBaseFTPUrl();
			string FTPUserName = Config.GetRemoteUsername();
			string FTPPassword = Config.GetRemotePassword();

			try
			{
				FtpWebRequest plainRequest = CreateFtpWebRequest(FTPURL, FTPUserName, FTPPassword);

				plainRequest.Method = WebRequestMethods.Ftp.ListDirectory;
				plainRequest.Timeout = 4000;

				try
				{
					using (WebResponse response = plainRequest.GetResponse())
					{
						bCanConnectToFTP = true;
					}					
				}
				catch (WebException wex)
				{
					Console.WriteLine("WebException in FTPProtocolHandler.CanPatch(): " + wex.Message);
					bCanConnectToFTP = false;
				}
			}
			catch (WebException wex)
			{
				// Case where FTP URL in config is not valid
				Console.WriteLine("WebException FTPProtocolHandler.CanPatch() (Invalid URL): " + wex.Message);

				bCanConnectToFTP = false;
			}

			if (!bCanConnectToFTP)
			{
				Console.WriteLine("Failed to connect to FTP server at: {0}", Config.GetBaseFTPUrl());
			}

			return bCanConnectToFTP;
		}

		public override bool IsPlatformAvailable(ESystemTarget Platform)
		{
			string remote = String.Format("{0}/game/{1}/.provides",
				                Config.GetBaseFTPUrl(),
				                Platform);

			return DoesRemoteFileExist(remote);
		}

		public override bool CanProvideChangelog()
		{
			return true;
		}

		public override string GetChangelog()
		{
			string changelogURL = String.Format("{0}/launcher/changelog.html", 
				                      Config.GetBaseFTPUrl());

			// Return simple raw HTML
			return ReadRemoteFile(changelogURL);
		}


		public override bool IsLauncherOutdated()
		{
			try
			{
				Version local = Config.GetLocalLauncherVersion();
				Version remote = GetRemoteLauncherVersion();	

				return local < remote;
			}
			catch (WebException wex)
			{
				Console.WriteLine("WebException in IsLauncherOutdated(): " + wex.Message);
				return false;	
			}
		}

		public override bool IsGameOutdated()
		{
			try
			{
				Version local = Config.GetLocalGameVersion();
				Version remote = GetRemoteGameVersion();

				return local < remote;
			}
			catch (WebException wex)
			{
				Console.WriteLine("WebException in IsGameOutdated(): " + wex.Message);
				return false;
			}
		}

		public override void InstallGame()
		{
			ModuleInstallFinishedArgs.Module = EModule.Game;
			ModuleInstallFailedArgs.Module = EModule.Game;

			try
			{
				//create the .install file to mark that an installation has begun
				//if it exists, do nothing.
				ConfigHandler.CreateInstallCookie();

				// Make sure the manifest is up to date
				RefreshGameManifest();

				// Download Game
				DownloadGame();

				// Verify Game
				VerifyGame();
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in InstallGame(): " + ioex.Message);
			}

			// OnModuleInstallationFinished and OnModuleInstallationFailed is in VerifyGame 
			// in order to allow it to run as a standalone action, while still keeping this functional.

			// As a side effect, it is required that it is the last action to run in Install and Update,
			// which happens to coincide with the general design.
		}

		public override void DownloadLauncher()
		{
			RefreshLaunchpadManifest();

			ModuleInstallFinishedArgs.Module = EModule.Launcher;
			ModuleInstallFailedArgs.Module = EModule.Launcher;

			List<ManifestEntry> Manifest = manifestHandler.LaunchpadManifest;

			//in order to be able to resume downloading, we check if there is an entry
			//stored in the install cookie.
			//attempt to parse whatever is inside the install cookie
			ManifestEntry lastDownloadedFile;
			if (ManifestEntry.TryParse(File.ReadAllText(ConfigHandler.GetInstallCookiePath()), out lastDownloadedFile))
			{
				//loop through all the entries in the manifest until we encounter
				//an entry which matches the one in the install cookie

				foreach (ManifestEntry Entry in Manifest)
				{
					if (lastDownloadedFile == Entry)
					{
						//remove all entries before the one we were last at.
						Manifest.RemoveRange(0, Manifest.IndexOf(Entry));
					}
				}
			}

			int downloadedFiles = 0;
			foreach (ManifestEntry Entry in Manifest)
			{
				++downloadedFiles;

				// Prepare the progress event contents
				ModuleDownloadProgressArgs.IndicatorLabelMessage = GetDownloadIndicatorLabelMessage(downloadedFiles, Path.GetFileName(Entry.RelativePath), Manifest.Count);
				OnModuleDownloadProgressChanged();

				DownloadEntry(Entry, EModule.Launcher);
			}

			VerifyLauncher();
		}

		protected override void DownloadGame()
		{
			List<ManifestEntry> Manifest = manifestHandler.GameManifest;

			//in order to be able to resume downloading, we check if there is an entry
			//stored in the install cookie.
			//attempt to parse whatever is inside the install cookie
			ManifestEntry lastDownloadedFile;
			if (ManifestEntry.TryParse(File.ReadAllText(ConfigHandler.GetInstallCookiePath()), out lastDownloadedFile))
			{
				//loop through all the entries in the manifest until we encounter
				//an entry which matches the one in the install cookie

				foreach (ManifestEntry Entry in Manifest)
				{
					if (lastDownloadedFile == Entry)
					{
						//remove all entries before the one we were last at.
						Manifest.RemoveRange(0, Manifest.IndexOf(Entry));
					}
				}
			}

			int downloadedFiles = 0;
			foreach (ManifestEntry Entry in Manifest)
			{
				++downloadedFiles;

				// Prepare the progress event contents
				ModuleDownloadProgressArgs.IndicatorLabelMessage = GetDownloadIndicatorLabelMessage(downloadedFiles, Path.GetFileName(Entry.RelativePath), Manifest.Count);
				OnModuleDownloadProgressChanged();

				DownloadEntry(Entry, EModule.Game);
			}
		}

		public override void VerifyLauncher()
		{
			try
			{
				List<ManifestEntry> Manifest = manifestHandler.LaunchpadManifest;			
				List<ManifestEntry> BrokenFiles = new List<ManifestEntry>();
							
				int verifiedFiles = 0;
				foreach (ManifestEntry Entry in Manifest)
				{					
					++verifiedFiles;

					// Prepare the progress event contents
					ModuleVerifyProgressArgs.IndicatorLabelMessage = GetVerifyIndicatorLabelMessage(verifiedFiles, Path.GetFileName(Entry.RelativePath), Manifest.Count);
					OnModuleVerifyProgressChanged();

					if (!Entry.IsFileIntegrityIntact())
					{
						BrokenFiles.Add(Entry);
					}
				}

				int downloadedFiles = 0;
				foreach (ManifestEntry Entry in BrokenFiles)
				{					
					++downloadedFiles;

					// Prepare the progress event contents
					ModuleDownloadProgressArgs.IndicatorLabelMessage = GetDownloadIndicatorLabelMessage(downloadedFiles, Path.GetFileName(Entry.RelativePath), BrokenFiles.Count);
					OnModuleDownloadProgressChanged();

					for (int i = 0; i < Config.GetFileRetries(); ++i)
					{					
						if (!Entry.IsFileIntegrityIntact())
						{
							DownloadEntry(Entry, EModule.Launcher);
						}
						else
						{
							break;
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in VerifyLauncher(): " + ioex.Message);
				OnModuleInstallationFailed();			
			}

			OnModuleInstallationFinished();
		}

		public override void VerifyGame()
		{
			try
			{
				List<ManifestEntry> Manifest = manifestHandler.GameManifest;			
				List<ManifestEntry> BrokenFiles = new List<ManifestEntry>();
							
				int verifiedFiles = 0;
				foreach (ManifestEntry Entry in Manifest)
				{					
					++verifiedFiles;

					// Prepare the progress event contents
					ModuleVerifyProgressArgs.IndicatorLabelMessage = GetVerifyIndicatorLabelMessage(verifiedFiles, Path.GetFileName(Entry.RelativePath), Manifest.Count);
					OnModuleVerifyProgressChanged();

					if (!Entry.IsFileIntegrityIntact())
					{
						BrokenFiles.Add(Entry);
					}
				}

				int downloadedFiles = 0;
				foreach (ManifestEntry Entry in BrokenFiles)
				{					
					++downloadedFiles;

					// Prepare the progress event contents
					ModuleDownloadProgressArgs.IndicatorLabelMessage = GetDownloadIndicatorLabelMessage(downloadedFiles, Path.GetFileName(Entry.RelativePath), BrokenFiles.Count);
					OnModuleDownloadProgressChanged();

					for (int i = 0; i < Config.GetFileRetries(); ++i)
					{					
						if (!Entry.IsFileIntegrityIntact())
						{
							DownloadEntry(Entry, EModule.Game);
						}
						else
						{
							break;
						}
					}
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in VerifyGame(): " + ioex.Message);
				OnModuleInstallationFailed();			
			}

			OnModuleInstallationFinished();
		}

		public override void UpdateGame()
		{
			// Make sure the local copy of the manifest is up to date
			RefreshGameManifest();

			//check all local files against the manifest for file size changes.
			//if the file is missing or the wrong size, download it.
			//better system - compare old & new manifests for changes and download those?
			List<ManifestEntry> Manifest = manifestHandler.GameManifest;
			List<ManifestEntry> OldManifest = manifestHandler.OldGameManifest;

			List<ManifestEntry> FilesRequiringUpdate = new List<ManifestEntry>();
			foreach (ManifestEntry Entry in Manifest)
			{
				if (!OldManifest.Contains(Entry))
				{
					FilesRequiringUpdate.Add(Entry);
				}
			}

			try
			{
				int updatedFiles = 0;
				foreach (ManifestEntry Entry in FilesRequiringUpdate)
				{
					++updatedFiles;

					ModuleUpdateProgressArgs.IndicatorLabelMessage = GetUpdateIndicatorLabelMessage(Path.GetFileName(Entry.RelativePath), 
						updatedFiles, 
						FilesRequiringUpdate.Count);
					OnModuleUpdateProgressChanged();

					DownloadEntry(Entry, EModule.Game);
				}
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in UpdateGameAsync(): " + ioex.Message);
				OnModuleInstallationFailed();
			}

			OnModuleInstallationFinished();
		}

		/// <summary>
		/// Downloads the provided manifest entry.
		/// This function resumes incomplete files, verifies downloaded files and 
		/// downloads missing files.
		/// </summary>
		/// <param name="Entry">The entry to download.</param>
		/// <param name="Module">The module to download the file for. Determines download location.</param>
		private void DownloadEntry(ManifestEntry Entry, EModule Module)
		{
			ModuleDownloadProgressArgs.Module = Module;

			string baseRemotePath;
			string baseLocalPath;
			if (Module == EModule.Game)
			{
				baseRemotePath = Config.GetGameURL();
				baseLocalPath = Config.GetGamePath();
			}
			else
			{
				baseRemotePath = Config.GetLauncherBinariesURL();
				baseLocalPath = ConfigHandler.GetTempLauncherDownloadPath();
			}

			string RemotePath = String.Format("{0}{1}", 
				                    baseRemotePath, 
				                    Entry.RelativePath);

			string LocalPath = String.Format("{0}{1}{2}", 
				                   baseLocalPath,
				                   Path.DirectorySeparatorChar, 
				                   Entry.RelativePath);
					                   				
			// Make sure we have a directory to put the file in
			Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));
				
			// Reset the cookie
			File.WriteAllText(ConfigHandler.GetInstallCookiePath(), String.Empty);

			// Write the current file progress to the install cookie
			using (TextWriter textWriterProgress = new StreamWriter(ConfigHandler.GetInstallCookiePath()))
			{					
				textWriterProgress.WriteLine(Entry);
				textWriterProgress.Flush();
			}

			if (File.Exists(LocalPath))
			{
				FileInfo fileInfo = new FileInfo(LocalPath);
				if (fileInfo.Length != Entry.Size)
				{
					// If the file is partial, resume the download.
					if (fileInfo.Length < Entry.Size)
					{
						DownloadRemoteFile(RemotePath, LocalPath, fileInfo.Length);
					}
					else
					{
						// If it's larger than expected, toss it in the bin and try again.
						File.Delete(LocalPath);
						DownloadRemoteFile(RemotePath, LocalPath);
					}
				}
				else
				{
					string LocalHash;
					using (FileStream fs = File.OpenRead(LocalPath))
					{
						LocalHash = MD5Handler.GetStreamHash(fs);
					}

					if (LocalHash != Entry.Hash)
					{
						File.Delete(LocalPath);
						DownloadRemoteFile(RemotePath, LocalPath);
					}
				}								
			}
			else
			{
				//no file, download it
				DownloadRemoteFile(RemotePath, LocalPath);
			}		

			if (ChecksHandler.IsRunningOnUnix())
			{
				//if we're dealing with a file that should be executable, 
				string gameName = Config.GetGameName();
				bool bFileIsGameExecutable = (Path.GetFileName(LocalPath).EndsWith(".exe")) || (Path.GetFileName(LocalPath) == gameName);
				if (bFileIsGameExecutable)
				{
					//set the execute bits
					UnixHandler.MakeExecutable(LocalPath);
				}					
			}

			// We've finished the download, so empty the cookie
			File.WriteAllText(ConfigHandler.GetInstallCookiePath(), String.Empty);
		}

		/// <summary>
		/// Gets the indicator label message to display to the user while installing.
		/// </summary>
		/// <returns>The indicator label message.</returns>
		/// <param name="nFilesDownloaded">N files downloaded.</param>
		/// <param name="currentFilename">Current filename.</param>
		/// <param name="totalFilesToDownload">Total files to download.</param>
		private string GetDownloadIndicatorLabelMessage(int nFilesDownloaded, string currentFilename, int totalFilesToDownload)
		{
			return String.Format("Downloading file {0} ({1} of {2})", currentFilename, nFilesDownloaded, totalFilesToDownload);
		}

		/// <summary>
		/// Gets the indicator label message to display to the user while repairing.
		/// </summary>
		/// <returns>The indicator label message.</returns>
		/// <param name="nFilesToVerify">N files downloaded.</param>
		/// <param name="currentFilename">Current filename.</param>
		/// <param name="totalFilesVerified">Total files to download.</param>
		private string GetVerifyIndicatorLabelMessage(int nFilesToVerify, string currentFilename, int totalFilesVerified)
		{
			return String.Format("Verifying file {0} ({1} of {2})", currentFilename, nFilesToVerify, totalFilesVerified);
		}

		/// <summary>
		/// Gets the indicator label message to display to the user while repairing.
		/// </summary>
		/// <returns>The indicator label message.</returns>	
		/// <param name="currentFilename">Current filename.</param>
		/// <param name="updatedFiles">Number of files that have been updated</param>
		/// <param name="totalFiles">Total files that are to be updated</param>
		private string GetUpdateIndicatorLabelMessage(string currentFilename, int updatedFiles, int totalFiles)
		{
			return String.Format("Verifying file {0} ({1} of {2})", currentFilename, updatedFiles, totalFiles);
		}

		/// <summary>
		/// Reads a text file from a remote FTP server.
		/// </summary>
		/// <returns>The FTP file contents.</returns>
		/// <param name="rawRemoteURL">FTP file path.</param>
		/// <param name="useAnonymousLogin">Force anonymous credentials for the connection.</param>
		public string ReadRemoteFile(string rawRemoteURL, bool useAnonymousLogin = false)
		{			
			// Clean the input URL first
			string remoteURL = rawRemoteURL.Replace(Path.DirectorySeparatorChar, '/');

			string username;
			string password;
			if (useAnonymousLogin)
			{
				username = "anonymous";
				password = "anonymous";

			}
			else
			{
				username = Config.GetRemoteUsername();
				password = Config.GetRemotePassword();
			}		

			try
			{
				FtpWebRequest request = CreateFtpWebRequest(remoteURL, username, password);
				FtpWebRequest sizerequest = CreateFtpWebRequest(remoteURL, username, password);

				request.Method = WebRequestMethods.Ftp.DownloadFile;
				sizerequest.Method = WebRequestMethods.Ftp.GetFileSize;
				           
				string data = "";
				using (Stream remoteStream = request.GetResponse().GetResponseStream())
				{
					long fileSize;
					using (FtpWebResponse sizeResponse = (FtpWebResponse)sizerequest.GetResponse())
					{
						fileSize = sizeResponse.ContentLength;
					}

					if (fileSize < 4096)
					{
						byte[] smallBuffer = new byte[fileSize];
						remoteStream.Read(smallBuffer, 0, smallBuffer.Length);

						data = Encoding.UTF8.GetString(smallBuffer, 0, smallBuffer.Length);
					}
					else
					{
						byte[] buffer = new byte[4096];

						while (true)
						{
							int bytesRead = remoteStream.Read(buffer, 0, buffer.Length);

							if (bytesRead == 0)
							{
								break;
							}

							data += Encoding.UTF8.GetString(buffer, 0, bytesRead);
						}
					}
				}

				//clean the output from \n and \0, then return
				return Utilities.Clean(data);
			}
			catch (WebException wex)
			{
				Console.Write("WebException in ReadRemoteFileException: ");
				Console.WriteLine(wex.Message + " (" + remoteURL + ")");
				return wex.Message;
			}
		}

		/// <summary>
		/// Gets the relative paths for all files in the specified FTP directory.
		/// </summary>
		/// <param name="rawRemoteURL">The URL to search.</param>
		/// <param name="bRecursively">Should the search should include subdirectories?</param>
		/// <returns>A list of relative paths for the files in the specified directory.</returns>
		public List<string> GetFilePaths(string rawRemoteURL, bool bRecursively)
		{			
			string remoteURL = Utilities.Clean(rawRemoteURL) + "/";
			List<string> relativePaths = new List<string>();

			if (DoesRemoteDirectoryExist(remoteURL))
			{
				try
				{
					FtpWebRequest request = CreateFtpWebRequest(
						                        remoteURL, 
						                        Config.GetRemoteUsername(), 
						                        Config.GetRemotePassword());

					request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

					using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
					{
						using (StreamReader sr = new StreamReader(response.GetResponseStream()))
						{
							string rawListing = sr.ReadToEnd();
							string[] listing = rawListing.Replace("\r", String.Empty).Split('\n');

							List<string> directories = new List<string>();
							foreach (string fileOrDir in listing)
							{
								// We only need to save the directories if we're searching recursively
								if (bRecursively && fileOrDir.StartsWith("d"))
								{
									// It's a directory, add it to directories
									string[] parts = fileOrDir.Split(' ');                        
									string relativeDirectoryPath = parts[parts.Length - 1];

									directories.Add(relativeDirectoryPath);
								}
								else
								{
									// There's a file, add it to our relative paths
									string[] filePath = fileOrDir.Split(' ');
									if (!String.IsNullOrEmpty(filePath[filePath.Length - 1]))
									{
										string relativePath = "/" + filePath[filePath.Length - 1];
										relativePaths.Add(relativePath);
									}                        
								}
							}

							// If we should search recursively, keep looking in subdirectories.
							if (bRecursively)
							{
								if (directories.Count != 0)
								{
									foreach (string directory in directories)
									{
										string parentDirectory = remoteURL.Replace(Config.GetLauncherBinariesURL(), String.Empty);

										string recursiveURL = Config.GetLauncherBinariesURL() + parentDirectory + "/" + directory;
										List<string> files = GetFilePaths(recursiveURL, true);
										foreach (string rawPath in files)
										{
											string relativePath = "/" + directory + rawPath;
											relativePaths.Add(relativePath);
										}
									}
								}
							}
						}
					}
				}
				catch (WebException wex)
				{
					Console.WriteLine("WebException in GetFileURLs(): " + wex.Message);
					return null;
				}
			}	

			return relativePaths;
		}

		/// <summary>
		/// Downloads an FTP file.
		/// </summary>
		/// <returns>The FTP file's location on disk, or the exception message.</returns>
		/// <param name="URL">Ftp source file path.</param>
		/// <param name="localPath">Local destination.</param>
		/// <param name="contentOffset">Offset into the remote file where downloading should start</param>
		/// <param name="useAnonymousLogin">If set to <c>true</c> b use anonymous.</param>
		public void DownloadRemoteFile(string URL, string localPath, long contentOffset = 0, bool useAnonymousLogin = false)
		{
			//clean the URL string
			string remoteURL = URL.Replace(Path.DirectorySeparatorChar, '/');

			string username;
			string password;
			if (useAnonymousLogin)
			{
				username = "anonymous";
				password = "anonymous";
			}
			else
			{
				username = Config.GetRemoteUsername();
				password = Config.GetRemotePassword();
			}

			try
			{
				FtpWebRequest request = CreateFtpWebRequest(remoteURL, username, password);
				FtpWebRequest sizerequest = CreateFtpWebRequest(remoteURL, username, password);

				request.Method = WebRequestMethods.Ftp.DownloadFile;
				request.ContentOffset = contentOffset;

				sizerequest.Method = WebRequestMethods.Ftp.GetFileSize;

				using (Stream contentStream = request.GetResponse().GetResponseStream())
				{
					long fileSize = contentOffset;
					using (FtpWebResponse sizereader = (FtpWebResponse)sizerequest.GetResponse())
					{
						fileSize += sizereader.ContentLength;
					}

					using (FileStream fileStream = contentOffset > 0 ? new FileStream(localPath, FileMode.Append) :
																		new FileStream(localPath, FileMode.Create))
					{
						fileStream.Position = contentOffset;
						long totalBytesDownloaded = contentOffset;

						if (fileSize < 4096)
						{
							byte[] smallBuffer = new byte[fileSize];
							contentStream.Read(smallBuffer, 0, smallBuffer.Length);

							fileStream.Write(smallBuffer, 0, smallBuffer.Length);

							totalBytesDownloaded += smallBuffer.Length;

							// Report download progress
							ModuleDownloadProgressArgs.ProgressBarMessage = GetDownloadProgressBarMessage(Path.GetFileName(remoteURL), 
								totalBytesDownloaded, fileSize);
							ModuleDownloadProgressArgs.ProgressFraction = (double)totalBytesDownloaded / (double)fileSize;
							OnModuleDownloadProgressChanged();
						}
						else
						{
							byte[] buffer = new byte[4096];

							while (true)
							{
								int bytesRead = contentStream.Read(buffer, 0, buffer.Length);

								if (bytesRead == 0)
								{
									break;
								}

								fileStream.Write(buffer, 0, bytesRead);

								totalBytesDownloaded += bytesRead;

								// Report download progress
								ModuleDownloadProgressArgs.ProgressBarMessage = GetDownloadProgressBarMessage(Path.GetFileName(remoteURL), 
									totalBytesDownloaded, fileSize);
								ModuleDownloadProgressArgs.ProgressFraction = (double)totalBytesDownloaded / (double)fileSize;
								OnModuleDownloadProgressChanged();
							}
						}
					}
				}
			}
			catch (WebException wex)
			{
				Console.Write("WebException in DownloadRemoteFile: ");
				Console.WriteLine(wex.Message + " (" + remoteURL + ")");
			}
			catch (IOException ioex)
			{
				Console.Write("IOException in DownloadRemoteFile: ");
				Console.WriteLine(ioex.Message + " (" + remoteURL + ")");
			}
		}

		/// <summary>
		/// Gets the progress bar message.
		/// </summary>
		/// <returns>The progress bar message.</returns>
		/// <param name="filename">Filename.</param>
		/// <param name="downloadedBytes">Downloaded bytes.</param>
		/// <param name="totalBytes">Total bytes.</param>
		private string GetDownloadProgressBarMessage(string filename, long downloadedBytes, long totalBytes)
		{
			return String.Format("Downloading {0}: {1} out of {2} bytes", filename, downloadedBytes, totalBytes);
		}

		/// <summary>
		/// Creates an ftp web request.
		/// </summary>
		/// <returns>The ftp web request.</returns>
		/// <param name="ftpDirectoryPath">Ftp directory path.</param>
		/// <param name="username">Remote FTP username.</param>
		/// <param name="password">Remote FTP password</param>
		public static FtpWebRequest CreateFtpWebRequest(string ftpDirectoryPath, string username, string password)
		{
			try
			{
				FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri(ftpDirectoryPath));

				//Set proxy to null. Under current configuration if this option is not set then the proxy 
				//that is used will get an html response from the web content gateway (firewall monitoring system)
				request.Proxy = null;

				request.UsePassive = true;
				request.UseBinary = true;

				request.Credentials = new NetworkCredential(username, password);

				return request;
			}
			catch (WebException wex)
			{
				Console.WriteLine("WebException in CreateFTPWebRequest(): " + wex.Message);

				return null;
			}
			catch (ArgumentException aex)
			{
				Console.WriteLine("ArgumentException in CreateFTPWebRequest(): " + aex.Message);

				return null;
			}
            
		}

		/// <summary>
		/// Refreshs the local copy of the launchpad manifest.
		/// </summary>
		private void RefreshGameManifest()
		{
			if (File.Exists(ManifestHandler.GetGameManifestPath()))
			{
				if (IsGameManifestOutdated())
				{
					DownloadGameManifest();
				}
			}
			else
			{
				DownloadGameManifest();
			}
		}

		/// <summary>
		/// Refreshs the local copy of the launchpad manifest.
		/// </summary>
		private void RefreshLaunchpadManifest()
		{
			if (File.Exists(ManifestHandler.GetLaunchpadManifestPath()))
			{
				if (IsLaunchpadManifestOutdated())
				{
					DownloadLaunchpadManifest();
				}
			}
			else
			{
				DownloadLaunchpadManifest();
			}
		}

		/// <summary>
		/// Determines whether the game manifest is outdated.
		/// </summary>
		/// <returns><c>true</c> if the manifest is outdated; otherwise, <c>false</c>.</returns>
		private bool IsGameManifestOutdated()
		{
			if (File.Exists(ManifestHandler.GetGameManifestPath()))
			{
				string remoteHash = GetRemoteGameManifestChecksum();

				using (Stream file = File.OpenRead(ManifestHandler.GetGameManifestPath()))
				{
					string localHash = MD5Handler.GetStreamHash(file);

					return remoteHash != localHash;
				}
			}
			else
			{
				return true;
			}
		}

		/// <summary>
		/// Determines whether the launchpad manifest is outdated.
		/// </summary>
		/// <returns><c>true</c> if the manifest is outdated; otherwise, <c>false</c>.</returns>
		private bool IsLaunchpadManifestOutdated()
		{
			if (File.Exists(ManifestHandler.GetLaunchpadManifestPath()))
			{
				string remoteHash = GetRemoteLaunchpadManifestChecksum();

				using (Stream file = File.OpenRead(ManifestHandler.GetLaunchpadManifestPath()))
				{
					string localHash = MD5Handler.GetStreamHash(file);

					return remoteHash != localHash;
				}
			}
			else
			{
				return true;
			}
		}

		/// <summary>
		/// Downloads the game manifest.
		/// </summary>
		private void DownloadGameManifest()
		{
			try
			{
				string RemoteURL = manifestHandler.GetGameManifestURL();
				string LocalPath = ManifestHandler.GetGameManifestPath();
				string OldLocalPath = ManifestHandler.GetOldGameManifestPath();

				if (File.Exists(ManifestHandler.GetGameManifestPath()))
				{
					// Create a backup of the old manifest so that we can compare them when updating the game
					if (File.Exists(OldLocalPath))
					{
						File.Delete(OldLocalPath);
					}

					File.Move(LocalPath, OldLocalPath);			
				}						

				DownloadRemoteFile(RemoteURL, LocalPath);
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in DownloadGameManifest(): " + ioex.Message);
			}
		}

		/// <summary>
		/// Downloads the launchpad manifest.
		/// </summary>
		private void DownloadLaunchpadManifest()
		{
			try
			{
				string RemoteURL = manifestHandler.GetLaunchpadManifestURL();
				string LocalPath = ManifestHandler.GetLaunchpadManifestPath();
				string OldLocalPath = ManifestHandler.GetOldLaunchpadManifestPath();

				if (File.Exists(LocalPath))
				{
					// Create a backup of the old manifest so that we can compare them when updating the game
					if (File.Exists(OldLocalPath))
					{
						File.Delete(OldLocalPath);
					}

					File.Move(LocalPath, OldLocalPath);			
				}						

				DownloadRemoteFile(RemoteURL, LocalPath);
			}
			catch (IOException ioex)
			{
				Console.WriteLine("IOException in DownloadLaunchpadManifest(): " + ioex.Message);
			}
		}

		/// <summary>
		/// Gets the remote launcher version.
		/// </summary>
		/// <returns>The remote launcher version. 
		/// If the version could not be retrieved from the server, a version of 0.0.0 is returned.</returns>
		public Version GetRemoteLauncherVersion()
		{			
			string remoteVersionPath = Config.GetLauncherVersionURL();

			// Config.GetDoOfficialUpdates is used here since the official update server always allows anonymous logins.
			string remoteVersion = ReadRemoteFile(remoteVersionPath, Config.GetDoOfficialUpdates());

			Version version;
			if (Version.TryParse(remoteVersion, out version))
			{
				return version;
			}
			else
			{
				return new Version("0.0.0");
			}
		}

		//TODO: Maybe move to ManifestHandler?
		/// <summary>
		/// Gets the remote game version.
		/// </summary>
		/// <returns>The remote game version.</returns>
		public Version GetRemoteGameVersion()
		{
			string remoteVersionPath = String.Format("{0}/game/{1}/bin/GameVersion.txt", 
				                           Config.GetBaseFTPUrl(), 
				                           Config.GetSystemTarget());

			string remoteVersion = ReadRemoteFile(remoteVersionPath);

			Version version;
			if (Version.TryParse(remoteVersion, out version))
			{
				return version;
			}
			else
			{
				return new Version("0.0.0");
			}
		}

		//TODO: Maybe move to ManifestHandler?
		/// <summary>
		/// Gets the remote game manifest checksum.
		/// </summary>
		/// <returns>The remote game manifest checksum.</returns>
		public string GetRemoteGameManifestChecksum()
		{
			string checksum = ReadRemoteFile(manifestHandler.GetGameManifestChecksumURL());
			checksum = Utilities.Clean(checksum);

			return checksum;
		}

		/// <summary>
		/// Gets the remote launchpad manifest checksum.
		/// </summary>
		/// <returns>The remote launchpad manifest checksum.</returns>
		public string GetRemoteLaunchpadManifestChecksum()
		{
			string checksum = ReadRemoteFile(manifestHandler.GetLaunchpadManifestChecksumURL());
			checksum = Utilities.Clean(checksum);

			return checksum;
		}

		/// <summary>
		/// Checks if a given directory exists on the remote FTP server.
		/// </summary>
		/// <returns><c>true</c>, if the directory exists, <c>false</c> otherwise.</returns>
		/// <param name="remotePath">Remote path.</param>
		public bool DoesRemoteDirectoryExist(string remotePath)
		{
			FtpWebRequest request = CreateFtpWebRequest(remotePath, 
				                        Config.GetRemoteUsername(),
				                        Config.GetRemotePassword());
			FtpWebResponse response = null;

			try
			{
				request.Method = WebRequestMethods.Ftp.ListDirectory;
				response = (FtpWebResponse)request.GetResponse();
			}
			catch (WebException ex)
			{
				response = (FtpWebResponse)ex.Response;
				if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
				{
					return false;
				}
			}
			finally
			{
				if (response != null)
				{
					response.Dispose();
				}
			}

			return true;
			
		}

		/// <summary>
		/// Checks if a given file exists on the remote FTP server.
		/// </summary>
		/// <returns><c>true</c>, if the file exists, <c>false</c> otherwise.</returns>
		/// <param name="remotePath">Remote path.</param>
		public bool DoesRemoteFileExist(string remotePath)
		{
			FtpWebRequest request = CreateFtpWebRequest(remotePath, 
				                        Config.GetRemoteUsername(),
				                        Config.GetRemotePassword());
			FtpWebResponse response = null;

			try
			{
				response = (FtpWebResponse)request.GetResponse();
			}
			catch (WebException ex)
			{
				response = (FtpWebResponse)ex.Response;
				if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
				{
					return false;
				}
			}
			finally
			{
				if (response != null)
				{
					response.Dispose();
				}
			}

			return true;
		}
	}
}
