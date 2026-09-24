/*
  Copyright (c) Microsoft Corporation. All rights reserved.
  Licensed under the MIT License. See License.txt in the project root for license information.
*/

namespace Adxstudio.Xrm.IO
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Polly;
	using Adxstudio.Xrm.Threading;

	/// <summary>
	/// Helpers related to <see cref="System.IO.File"/> and <see cref="System.IO.Directory"/>.
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// A <see cref="Func{Exception, bool}"/> for App_Data transient errors.
		/// </summary>
		private class AppDataTransientErrorDetectionStrategy
		{
			/// <summary>
			/// Flags transient errors.
			/// </summary>
			/// <param name="ex">The error.</param>
			/// <returns>'true' if the error is transient.</returns>
			public bool IsTransient(Exception ex)
			{
				return ex is IOException
					|| ex is UnauthorizedAccessException;
			}
		}

		/// <summary>
		/// Creates a default <see cref="ResiliencePipeline"/>.
		/// </summary>
		/// <param name="retryStrategy">The retry strategy.</param>
		/// <returns>The retry policy.</returns>
		public static ResiliencePipeline CreateRetryPolicy(this IEnumerable<TimeSpan> retryStrategy)
		{
			var detectionStrategy = new AppDataTransientErrorDetectionStrategy();
			return RetryPolicies.Create(detectionStrategy.IsTransient, retryStrategy);
		}

		/// <summary>
		/// Creates a <see cref="FileSystemWatcher"/> with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>The watcher.</returns>
		public static FileSystemWatcher CreateFileSystemWatcher(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => new FileSystemWatcher(path)
			{
				InternalBufferSize = 1024 * 64,
				NotifyFilter = NotifyFilters.FileName,
				EnableRaisingEvents = true,
				IncludeSubdirectories = false,
			});
		}

		/// <summary>
		/// Creates a <see cref="DirectoryInfo"/> with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>The directory.</returns>
		public static DirectoryInfo GetDirectory(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => new DirectoryInfo(path));
		}

		/// <summary>
		/// Calls Exists method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>'true' if the directory exists.</returns>
		public static bool DirectoryExists(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => Directory.Exists(path));
		}

		/// <summary>
		/// Calls CreateDirectory method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>The directory.</returns>
		public static DirectoryInfo DirectoryCreate(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => Directory.CreateDirectory(path));
		}

		/// <summary>
		/// Calls Delete method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <param name="recursive">The flag to delete subdirectories.</param>
		public static void DirectoryDelete(this ResiliencePipeline retryPolicy, string path, bool recursive)
		{
			retryPolicy.Execute(() => Directory.Delete(path, recursive));
		}

		/// <summary>
		/// Calls GetDirectories method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="directory">The directory.</param>
		/// <param name="searchPattern">The filter.</param>
		/// <returns>The directories.</returns>
		public static DirectoryInfo[] GetDirectories(this ResiliencePipeline retryPolicy, DirectoryInfo directory, string searchPattern)
		{
			return retryPolicy.Execute(() => directory.GetDirectories(searchPattern));
		}

		/// <summary>
		/// Calls GetFiles method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="directory">The directory.</param>
		/// <param name="searchPattern">The filter.</param>
		/// <returns>The files.</returns>
		public static FileInfo[] GetFiles(this ResiliencePipeline retryPolicy, DirectoryInfo directory, string searchPattern)
		{
			return retryPolicy.Execute(() => directory.GetFiles(searchPattern));
		}

		/// <summary>
		/// Calls Exists method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>'true' if the file exists.</returns>
		public static bool FileExists(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => File.Exists(path));
		}

		/// <summary>
		/// Calls Move method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="sourceFileName">The source path.</param>
		/// <param name="destFileName">The destination path.</param>
		public static void FileMove(this ResiliencePipeline retryPolicy, string sourceFileName, string destFileName)
		{
			retryPolicy.Execute(() => File.Move(sourceFileName, destFileName));
		}

		/// <summary>
		/// Calls Delete method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		public static void FileDelete(this ResiliencePipeline retryPolicy, string path)
		{
			retryPolicy.Execute(() => File.Delete(path));
		}

		/// <summary>
		/// Calls Delete method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="file">The file.</param>
		public static void FileDelete(this ResiliencePipeline retryPolicy, FileInfo file)
		{
			retryPolicy.Execute(file.Delete);
		}

		/// <summary>
		/// Calls Open method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <param name="mode">The access mode.</param>
		/// <returns>The file stream.</returns>
		public static FileStream Open(this ResiliencePipeline retryPolicy, string path, FileMode mode)
		{
			return retryPolicy.Execute(() => File.Open(path, mode));
		}

		/// <summary>
		/// Calls OpenText method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>The file stream.</returns>
		public static TextReader OpenText(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => File.OpenText(path));
		}

		/// <summary>
		/// Calls WriteAllBytes method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <param name="bytes">The data.</param>
		public static void WriteAllBytes(this ResiliencePipeline retryPolicy, string path, byte[] bytes)
		{
			retryPolicy.Execute(() => File.WriteAllBytes(path, bytes));
		}

		/// <summary>
		/// Calls ReadAllBytes method with retries.
		/// </summary>
		/// <param name="retryPolicy">The retry policy.</param>
		/// <param name="path">The path.</param>
		/// <returns>The data.</returns>
		public static byte[] ReadAllBytes(this ResiliencePipeline retryPolicy, string path)
		{
			return retryPolicy.Execute(() => File.ReadAllBytes(path));
		}
	}
}
