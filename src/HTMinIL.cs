using System.Text.RegularExpressions;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RivalRebels.Web
{
	public class HTMinIL
	{
		// Regexes to perform the HTML minification
		Regex flattenSpaces    = new Regex(@"\s+", RegexOptions.Compiled);
		Regex collapseBracket  = new Regex(@"(?:(?<=^|>) (?=<))|(?:(?<=>) (?=<|$))", RegexOptions.Compiled);
		Regex selfClosingSpace = new Regex(@" (?=/>)", RegexOptions.Compiled);
		Regex htmlComments     = new Regex(@"<!--(?!\[if ).*(?<!\[endif\])-->", RegexOptions.Compiled | RegexOptions.Singleline);

		// Instruction cache to keep track of empty WriteLiteral calls
		List<Instruction> toBeRemoved = new List<Instruction>();

		// Statistics to print a summary of the minification
		ulong totalCharactersInput  = 0;
		ulong totalCharactersOutput = 0;
		ulong eliminatedFunctions   = 0;
		ulong processedFiles        = 0;

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
				// Find IL code like:
				// IL_xx: ldstr "\n                </td>\n                <td>\n                    <h2>"
				// IL_xx: callvirt instance void [Microsoft.AspNetCore.Mvc.Razor]Microsoft.AspNetCore.Mvc.Razor.RazorPageBase::WriteLiteral(string)

				if (op.OpCode.Code == Code.Ldstr
				 && op.Operand is string html
				 && op.Next != null
				 && op.Next.OpCode.Code == Code.Callvirt
				 && op.Next.Operand is MethodReference calledMethod
				 && calledMethod.Name == "WriteLiteral")
				{
					// Minify the string.
					string htminil = Minify(html);

					// Keep track of the reduction for statistics.
					totalCharactersInput  += (ulong) html.Length;
					totalCharactersOutput += (ulong) htminil.Length;

					// Replace the original string with the minified string.
					op.Operand = htminil;

					// If the call is now empty, queue the instructions for removal.
					if (htminil == "")
					{
						// Keep track of how many WriteLiteral calls we've removed for statistics.
						eliminatedFunctions++;

						// Remove ldarg.0
						toBeRemoved.Add(op.Previous);
						// Remove ldstr
						toBeRemoved.Add(op);
						// Remove callvirt
						toBeRemoved.Add(op.Next);
					}
				}
			}

			// Remove all the queued instructions.
			foreach (Instruction op in toBeRemoved)
			{
				ilp.Remove(op);
			}
			// Clear the instruction list for the next use.
			toBeRemoved.Clear();
		}

		public string Minify(string html)
		{
			// Replaces multiple whitespaces with a single space. "<div>    \n<p>" => "<div> <p>"
			html = flattenSpaces.Replace(html, " ");
			// Removes spaces between HTML tags. "<div> <p>" => "<div><p>"
			html = collapseBracket.Replace(html, "");
			// Removes the space before the tag self closer. "<br />" => "<br/>"
			html = selfClosingSpace.Replace(html, "");
			// Removes HTML comments that are not IE conditionals. "<div><!-- comment --><p>" => "<div><p>"
			html = htmlComments.Replace(html, "");

			// Removes single-space printing.
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
}
