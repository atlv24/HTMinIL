using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class HTMinILTask : Task
{
	[Required]
	public string TargetDLL { get; set; }

	Regex flattenSpaces = new Regex(@"\s+", RegexOptions.Compiled);
	Regex collapseBracket = new Regex(@"(?:(?<=^|>) (?=<))|(?:(?<=>) (?=<|$))", RegexOptions.Compiled);
	Regex selfClosingSpace = new Regex(@" (?=/>)", RegexOptions.Compiled);
	Regex htmlComments = new Regex(@"<!--(?!\[if ).*(?<!\[endif\])-->", RegexOptions.Compiled | RegexOptions.Singleline);

	public string Minify(string html)
	{
		html = flattenSpaces.Replace(html, " ");
		html = collapseBracket.Replace(html, "");
		html = selfClosingSpace.Replace(html, "");
		html = htmlComments.Replace(html, "");

		if (html == " ") return "";

		return html;
	}

	public static MethodDefinition ResolveMethod(TypeDefinition typeDef, string methodName)
	{
		foreach (MethodDefinition method in typeDef.Methods)
		{
			if (method.Name == methodName)
			{
				return method;
			}
		}
		throw new MissingMethodException(methodName + " does not exist on type " + typeDef.Name);
	}

	public override bool Execute()
	{
		bool success = false;
		try
		{
			if (!File.Exists(TargetDLL))
			{
				throw new FileNotFoundException("Target DLL does not exist.");
			}
			DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();

			ReaderParameters readParams = new ReaderParameters { AssemblyResolver = assemblyResolver };
			using (AssemblyDefinition viewAsmDef = AssemblyDefinition.ReadAssembly(TargetDLL, readParams))
			{
				ModuleDefinition viewModDef = viewAsmDef.MainModule;

				ulong totalCharactersInput = 0;
				ulong totalCharactersOutput = 0;
				ulong eliminatedFunctions = 0;

				foreach (TypeDefinition type in viewModDef.Types)
				{
					foreach (MethodDefinition method in type.Methods)
					{
						if (method.Name != "ExecuteAsync") continue;
						ILProcessor ilp = method.Body.GetILProcessor();
						foreach (Instruction op in method.Body.Instructions)
						{
							// find IL code like:
							// IL_xx: ldstr "\n                </td>\n                <td>\n                    <h2>"
							// IL_xx: callvirt instance void [Microsoft.AspNetCore.Mvc.Razor]Microsoft.AspNetCore.Mvc.Razor.RazorPageBase::WriteLiteral(string)

							if (op.OpCode.Code == Code.Ldstr
							 && op.Operand is string html
							 && op.Next != null
							 && op.Next.OpCode.Code == Code.Callvirt
							 && op.Next.Operand is MethodReference calledMethod
							 && calledMethod.Name == "WriteLiteral")
							{
								string htminil = Minify(html);
								totalCharactersInput += (ulong) html.Length;
								totalCharactersOutput += (ulong) htminil.Length;

								op.Operand = htminil;

								if (htminil == "")
								{
									eliminatedFunctions++;
									// TODO: remove call completely
								}
							}
						}
					}
				}

				viewAsmDef.Write(TargetDLL);

				double reduction = totalCharactersOutput / (double) totalCharactersInput;
				Log.LogMessage("HTMinIL Completed. Reduction: " + String.Format("Value: {0:P2}.", reduction) + " Eliminated Calls: " + eliminatedFunctions);
			}
			success = true;
		}
		catch (Exception e)
		{
			Log.LogError("HTMinIL Error: " + e.Message);
			success = false;
		}

		return success;
	}
}
