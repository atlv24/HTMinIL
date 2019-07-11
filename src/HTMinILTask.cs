using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace RivalRebels.Web
{
	public class HTMinILTask : Task
	{
		[Required]
		public string TargetDLL { get; set; }

		public override bool Execute()
		{
			bool success = false;
			try
			{
				if (!File.Exists(TargetDLL))
				{
					throw new FileNotFoundException(TargetDLL + " does not exist.");
				}
				DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();

				ReaderParameters readParams = new ReaderParameters { AssemblyResolver = assemblyResolver };
				using (AssemblyDefinition asmDef = AssemblyDefinition.ReadAssembly(TargetDLL, readParams))
				{
					HTMinIL htminil = new HTMinIL();
					htminil.ProcessAssembly(asmDef);

					asmDef.Write(TargetDLL + ".temp");
					File.Delete(TargetDLL + ".old");
					File.Move(TargetDLL, TargetDLL + ".old");
					File.Move(TargetDLL + ".temp", TargetDLL);

					Log.LogWarning(htminil.GetStatistics());
				}
				success = true;
			}
			catch (Exception e)
			{
				Log.LogError("HTMinIL Error: " + e);
				success = false;
			}

			return success;
		}
	}
}
