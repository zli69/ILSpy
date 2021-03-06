﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;

namespace ICSharpCode.Decompiler.IL.Transforms
{
	class NullableLiftingTransform
	{
		public static void Run(IfInstruction inst, ILTransformContext context)
		{
			// if (call Nullable<inputUType>.get_HasValue(ldloca v))
			//          newobj Nullable<utype>.ctor(...)
			// else
			//          default.value System.Nullable<utype>[[System.Int32]]
			if (MatchHasValueCall(inst.Condition, out var v)
				&& MatchNullableCtor(inst.TrueInst, out var utype, out var arg)
				&& MatchNull(inst.FalseInst, utype))
			{
				ILInstruction lifted;
				if (MatchGetValueOrDefault(arg, v)) {
					// v != null ? call GetValueOrDefault(ldloca v) : null
					// => conv.nop.lifted(ldloc v)
					// This case is handled separately from LiftUnary() because
					// that doesn't introduce nop-conversions.
					context.Step("if => conv.nop.lifted", inst);
					var inputUType = NullableType.GetUnderlyingType(v.Type);
					lifted = new Conv(new LdLoc(v), inputUType.GetStackType(), inputUType.GetSign(), utype.ToPrimitiveType(), false) {
						IsLifted = true,
						ILRange = inst.ILRange
					};
				} else {
					lifted = LiftUnary(arg, v, context);
				}
				if (lifted != null) {
					Debug.Assert(IsLifted(lifted));
					inst.ReplaceWith(lifted);
				}
			}
		}

		/// <summary>
		/// Lifting for unary expressions.
		/// Recurses into the instruction and checks that everything can be lifted,
		/// and that the chain ends with `call GetValueOrDefault(ldloca inputVar)`.
		/// </summary>
		static ILInstruction LiftUnary(ILInstruction inst, ILVariable inputVar, ILTransformContext context)
		{
			if (MatchGetValueOrDefault(inst, inputVar)) {
				// We found the end: the whole chain can be lifted.
				context.Step("NullableLiftingTransform.LiftUnary", inst);
				return new LdLoc(inputVar) { ILRange = inst.ILRange };
			} else if (inst is Conv conv) {
				var arg = LiftUnary(conv.Argument, inputVar, context);
				if (arg != null) {
					conv.Argument = arg;
					conv.IsLifted = true;
					return conv;
				}
			}
			return null;
		}

		static bool IsLifted(ILInstruction inst)
		{
			return inst is ILiftableInstruction liftable && liftable.IsLifted;
		}

		#region Match...Call
		/// <summary>
		/// Matches 'call get_HasValue(ldloca v)'
		/// </summary>
		static bool MatchHasValueCall(ILInstruction inst, out ILVariable v)
		{
			v = null;
			if (!(inst is Call call))
				return false;
			if (call.Arguments.Count != 1)
				return false;
			if (call.Method.Name != "get_HasValue")
				return false;
			if (call.Method.DeclaringTypeDefinition?.KnownTypeCode != KnownTypeCode.NullableOfT)
				return false;
			return call.Arguments[0].MatchLdLoca(out v);
		}

		/// <summary>
		/// Matches 'newobj Nullable{underlyingType}.ctor(arg)'
		/// </summary>
		static bool MatchNullableCtor(ILInstruction inst, out IType underlyingType, out ILInstruction arg)
		{
			underlyingType = null;
			arg = null;
			if (!(inst is NewObj newobj))
				return false;
			if (!newobj.Method.IsConstructor || newobj.Arguments.Count != 1)
				return false;
			if (newobj.Method.DeclaringTypeDefinition?.KnownTypeCode != KnownTypeCode.NullableOfT)
				return false;
			arg = newobj.Arguments[0];
			underlyingType = NullableType.GetUnderlyingType(newobj.Method.DeclaringType);
			return true;
		}

		/// <summary>
		/// Matches 'call Nullable{T}.GetValueOrDefault(arg)'
		/// </summary>
		static bool MatchGetValueOrDefault(ILInstruction inst, out ILInstruction arg)
		{
			arg = null;
			if (!(inst is Call call))
				return false;
			if (call.Method.Name != "GetValueOrDefault" || call.Arguments.Count != 1)
				return false;
			if (call.Method.DeclaringTypeDefinition?.KnownTypeCode != KnownTypeCode.NullableOfT)
				return false;
			arg = call.Arguments[0];
			return true;
		}

		/// <summary>
		/// Matches 'call Nullable{T}.GetValueOrDefault(ldloca v)'
		/// </summary>
		static bool MatchGetValueOrDefault(ILInstruction inst, out ILVariable v)
		{
			v = null;
			return MatchGetValueOrDefault(inst, out ILInstruction arg)
				&& arg.MatchLdLoca(out v);
		}

		/// <summary>
		/// Matches 'call Nullable{T}.GetValueOrDefault(ldloca v)'
		/// </summary>
		static bool MatchGetValueOrDefault(ILInstruction inst, ILVariable v)
		{
			return MatchGetValueOrDefault(inst, out ILVariable v2) && v == v2;
		}

		static bool MatchNull(ILInstruction inst, out IType underlyingType)
		{
			underlyingType = null;
			if (inst.MatchDefaultValue(out IType type)) {
				underlyingType = NullableType.GetUnderlyingType(type);
				return NullableType.IsNullable(type);
			}
			underlyingType = null;
			return false;
		}

		static bool MatchNull(ILInstruction inst, IType underlyingType)
		{
			return MatchNull(inst, out var utype) && utype.Equals(underlyingType);
		}
		#endregion
	}
}
