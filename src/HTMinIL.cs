using System.Text.RegularExpressions;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class HTMinIL
{
	// Regexes to perform the HTML minification
	Regex flattenSpaces = new Regex(@"\s+", RegexOptions.Compiled);
	Regex collapseBracket = new Regex(@"(?:(?<=^|>) (?=<))|(?:(?<=>) (?=<|$))", RegexOptions.Compiled);
	Regex selfClosingSpace = new Regex(@" (?=/>)", RegexOptions.Compiled);
	Regex htmlComments = new Regex(@"<!--(?!\[if ).*(?<!\[endif\])-->", RegexOptions.Compiled | RegexOptions.Singleline);

	// Instruction cache to keep track of empty WriteLiteral calls
	List<Instruction> toBeRemoved = new List<Instruction>();

	// Statistics to print a summary of the minification
	ulong totalCharactersInput = 0;
	ulong totalCharactersOutput = 0;
	ulong eliminatedFunctions = 0;
	ulong processedFiles = 0;

	public void ProcessAssembly(AssemblyDefinition asmDef)
	{
		ModuleDefinition modDef = asmDef.MainModule;

		// *.Views.dll have all the views as top level types
		foreach (TypeDefinition type in modDef.Types)
		{
			// The ExecuteAsync method is async, so it generates a nested type
			foreach (TypeDefinition nestedType in type.NestedTypes)
			{
				// Only process the generated nested types,
				// because there may be legitimate user-made nested types too.
				if (!nestedType.Name.Contains("ExecuteAsync")) continue;

				// Every method in the nested type must be processed,
				// since every await splits the method further.
				foreach (MethodDefinition method in nestedType.Methods)
				{
					ProcessMethod(method);
				}
			}

			// Keep track of how many views are processed for statistics.
			processedFiles++;
		}
	}

	public void ProcessMethod(MethodDefinition method)
	{
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
				totalCharactersInput += (ulong)html.Length;
				totalCharactersOutput += (ulong)htminil.Length;

				op.Operand = htminil;
				if (htminil == "")
				{
					eliminatedFunctions++;

					toBeRemoved.Add(op.Previous);
					toBeRemoved.Add(op.Next);
					toBeRemoved.Add(op);
				}
			}
		}

		foreach (Instruction op in toBeRemoved)
		{
			ilp.Remove(op);
		}
		toBeRemoved.Clear();
	}

	public string Minify(string html)
	{
		html = flattenSpaces.Replace(html, " ");
		html = collapseBracket.Replace(html, "");
		html = selfClosingSpace.Replace(html, "");
		html = htmlComments.Replace(html, "");

		if (html == " ") return "";

		return html;
	}

	public string GetStatistics()
	{
		double reduction = 1.0 - totalCharactersOutput / (double) totalCharactersInput;
		return "HTMinIL Completed."
		   + "\nProcessed Files : " + processedFiles
		   + "\nReduction       : " + string.Format("{0:P2}", reduction)
		   + "\nEliminated Calls: " + eliminatedFunctions;
	}
}
