//                                  ┌∩┐(◣_◢)┌∩┐
//																				\\
// MHierarchy.cs (30/05/2017)													\\
// Autor: Antonio Mateo (Moon Antonio) 	antoniomt.moon@gmail.com				\\
// Descripcion:		Editor de la jerarquia de Unity3D							\\
// Fecha Mod:		30/05/2017													\\
// Ultima Mod:		Version Inicial												\\
//******************************************************************************\\

#region Librerias
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;
#endregion

namespace MoonAntonio.MHierarchy
{
	#region Enums
	/// <summary>
	/// <para>Modo de entrada</para>
	/// </summary>
	[Flags]
	internal enum EntradaModo// Modo de entrada
	{
		ScriptingError = 256,
		ScriptingWarning = 512,
		ScriptingLog = 1024
	}

	/// <summary>
	/// <para>Tipo de label</para>
	/// </summary>
	internal enum TipoMiniLabel// Tipo de label
	{
		None = 0,
		Tag = 1,
		Layer = 2,
		TagOLayer = 3,
		LayerOTag = 4,
	}

	/// <summary>
	/// <para>Tipo de dibujado</para>
	/// </summary>
	internal enum DrawTipo// Tipo de dibujado
	{
		Activo = 0,
		Statico = 1,
		Lock = 2,
		Icon = 3,
		ApplyPrefab = 4,
		Tag = 5,
		Layer = 6
	}

	/// <summary>
	/// <para>Tipo de separadores</para>
	/// </summary>
	internal enum SeparadoresTipo// Tipo de separadores
	{
		Color = 1,
		Linea = 2,
		Objetos = 4
	}

	/// <summary>
	/// <para>Modo estatico</para>
	/// </summary>
	internal enum StaticMode// Modo estatico
	{
		Todo = 0,
		SoloObjeto = 1,
		Preguntar = 2,
	}
	#endregion

	/// <summary>
	/// <para>Clase principal, dibuja los iconos en la jerarquia</para>
	/// </summary>
	[InitializeOnLoad]
	[AddComponentMenu("MoonAntonio/MHierarchy/MHierarchy")]
	internal static class GUIDrawer
	{
		#region Variables
		private static int errorCount;
		private static bool isFirstVisible;
		private static bool hasTag;
		private static bool hasLayer;
		private static Event evt;
		private static GameObject go;
		private static Color currentColor;
		private static Vector2 selectionStart;
		private static Rect lastRect;
		private static Rect selectionRect;
		private static List<LogEntry> entries;
		private static List<GameObject> dragSelection;
		#endregion

		#region GUI
		/// <summary>
		/// <para>Inicializa la GUI</para>
		/// </summary>
		static GUIDrawer()// Inicializa la GUI
		{
			EditorApplication.hierarchyWindowItemOnGUI += ItemGUI;
		}
		#endregion

		#region Metodos
		/// <summary>
		/// <para>Item de la interfaz</para>
		/// </summary>
		/// <param name="instanceID">ID de la instancia</param>
		/// <param name="rect">Rectangulo</param>
		private static void ItemGUI(int instanceID, Rect rect)// Item de la interfaz
		{
			try
			{
				evt = Event.current;

				// Se puede cambiar o eliminar esta instruccion si esta en conflicto con otra extension
				if (evt.Equals(Event.KeyboardEvent("^h")))
				{
					Prefs.enabled = !Prefs.enabled;
					evt.Use();
				}

				// Comprobar si exta activada las preferencias
				if (!Prefs.enabled) return;

				// Asignar valores
				isFirstVisible = rect.y <= lastRect.y || lastRect.y == 0;
				go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

				// Comprobar si tenemos la id del objeto
				if (!go) return;

				// Asignar tags y layers
				hasTag = go.tag != "Untagged";
				hasLayer = LayerMask.LayerToName(go.layer) != "Default";
				currentColor = go.GetHierarchyColor();

				// Seleccionar rectangulo
				if (Prefs.selection) DoSeleccion(rect);

				// No mostrar las flags
				if (!Prefs.selectLocked && isFirstVisible)
				{
					var selection = Selection.objects;

					for (int i = 0; i < selection.Length; i++)
						if (selection[i] is GameObject && selection[i].hideFlags == (HideFlags.NotEditable | selection[i].hideFlags))
							selection[i] = null;

					Selection.objects = selection;
				}

				// Recordar los cambios
				Undo.RecordObject(go, "MHierarchy");

				// Dibujar dependiendo de las preferencias
				if (Prefs.lineSeparator) DrawHorizontalSeparador(rect);
				if (Prefs.colorSeparator && isFirstVisible) ColorSort(rect);
				if (Prefs.tree) DrawTree(rect);
				if (Prefs.warning) DrawWarnings(rect);
				else if (Prefs.trailing) DoTaponado(rect);

				// Fijar rect
				rect.xMin = rect.xMax - rect.height;
				rect.x += rect.height - Prefs.offset;
				rect.y++;

				// Dibujar el tipo
				for (int i = 0; i < Prefs.drawOrder.Count; i++)
				{
					rect.x -= rect.height;
					GUI.backgroundColor = Styles.backgroundColorEnabled;
					switch (Prefs.drawOrder[i])
					{
						case DrawTipo.Activo:
							DrawActiveBtn(rect);
							break;

						case DrawTipo.Statico:
							DrawStaticBtn(rect);
							break;

						case DrawTipo.Lock:
							DrawLockBtn(rect);
							break;

						case DrawTipo.Icon:
							DrawIcon(rect);
							break;

						case DrawTipo.ApplyPrefab:
							DrawPrefabApply(rect);
							break;

						case DrawTipo.Tag:
							DrawTag(rect);
							break;

						case DrawTipo.Layer:
							DrawLayer(rect);
							break;
					}

					if (Prefs.lineSeparator) DrawVerticalSeparador(rect);
				}

				DrawMiniLabel(ref rect);

				GUI.backgroundColor = Color.white;

				if (Prefs.tooltips)
					DrawTooltip(ref rect);

				lastRect = rect;
				lastRect.y--;
			}
			catch (Exception e)
			{
				Debug.LogErrorFormat("Excepción inesperada en MHierarchy: {0}", e);

				if (errorCount++ >= 3)
				{
					Debug.LogWarning("Desabilitando automaticamente MHierarchy.");
					Prefs.enabled = false;
					errorCount = 0;
				}
			}
		}

		/// <summary>
		/// <para>Dibuja el btn de estatico</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawStaticBtn(Rect rect)// Dibuja el btn de estatico
		{
			// Setup GUI
			GUI.changed = false;
			GUI.backgroundColor = go.isStatic ? Styles.backgroundColorDisabled : Styles.backgroundColorEnabled;
			GUI.Toggle(rect, go.isStatic, Styles.staticContent, Styles.staticToggleStyle);

			// Comprobar si la GUI a cambiado
			if (!GUI.changed) return;

			// Asignar var
			var changeMode = Prefs.staticAskMode;

			// Decision hijos o solo objeto
			if (go.transform.childCount == 0) changeMode = StaticMode.SoloObjeto;
			else if (changeMode == StaticMode.Preguntar)
			{
				var result = EditorUtility.DisplayDialogComplex("Cambiar Flags estaticas",
																"¿Quieres " + (go.isStatic ? "activar" : "desactivar") + " las flags estaticas para todos los hijos tambien?",
																"Si, Cambiar Hijos",
																"No, Solo este objeto",
																"Cancelar");

				if (result == 2)
					return;

				changeMode = (StaticMode)result;
			}

			// Asignar var
			var isStatic = !go.isStatic;

			// Eleccion
			switch (changeMode)
			{
				case StaticMode.SoloObjeto:
					go.isStatic = isStatic;
					break;

				case StaticMode.Todo:
					foreach (var transform in go.GetComponentsInChildren<Transform>())
						transform.gameObject.isStatic = isStatic;
					break;
			}
		}

		/// <summary>
		/// <para>Dibuja el btn de bloquear</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawLockBtn(Rect rect)// Dibuja el btn de bloquear
		{
			// Asignar var
			var locked = go.hideFlags == (go.hideFlags | HideFlags.NotEditable);

			// Setup GUI
			GUI.changed = false;
			GUI.backgroundColor = locked ? Styles.backgroundColorEnabled : Styles.backgroundColorDisabled;
			GUI.Toggle(rect, locked, Styles.lockContent, Styles.lockToggleStyle);

			// Comprobar si la GUI a cambiado
			if (!GUI.changed) return;

			// Bloquear
			go.hideFlags += locked ? -8 : 8;
			InternalEditorUtility.RepaintAllViews();
		}

		/// <summary>
		/// <para>Dibuja el btn de activar</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawActiveBtn(Rect rect)// Dibuja el btn de activar
		{
			// Setup GUI
			GUI.changed = false;
			GUI.backgroundColor = go.activeSelf ? Styles.backgroundColorEnabled : Styles.backgroundColorDisabled;
			GUI.Toggle(rect, go.activeSelf, Styles.activeContent, Styles.activeToggleStyle);

			// Comprobar si la GUI a cambiado
			if (GUI.changed) go.SetActive(!go.activeSelf);
		}

		/// <summary>
		/// <para>Dibuja el icono</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawIcon(Rect rect)// Dibuja el icono
		{
			// Asignar var
			var content = EditorGUIUtility.ObjectContent(go, typeof(GameObject));

			// Condicion
			if (!content.image) return;

			// Tooltip
			if (Prefs.tooltips) content.tooltip = "Cambiar Icono";

			content.text = string.Empty;
			rect.yMin++;
			rect.xMin++;

			if (GUI.Button(rect, content, EditorStyles.label)) Utility.ShowIconSelector(go, rect, true);
		}

		/// <summary>
		/// <para>Dibujar el aplicar prefab</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawPrefabApply(Rect rect)// Dibujar el aplicar prefab
		{
			// Asignar var
			var isPrefab = PrefabUtility.GetPrefabType(go) == PrefabType.PrefabInstance;

			// Setup GUI
			GUI.contentColor = isPrefab ? Styles.backgroundColorEnabled : Styles.backgroundColorDisabled;

			// Core
			if (GUI.Button(rect, Styles.prefabApplyContent, Styles.applyPrefabStyle))
			{
				if (isPrefab)
				{
					var selected = Selection.objects;
					Selection.activeGameObject = go;
					EditorApplication.ExecuteMenuItem("GameObject/Apply Changes To Prefab");
					Selection.objects = selected;
				}
				else
				{
					var path = EditorUtility.SaveFilePanelInProject("Guardar prefab", "newPrefab", "prefab", "Guardado el prefab seleccionado");
					if (path.Length > 0)
						PrefabUtility.CreatePrefab(path, go, ReplacePrefabOptions.ConnectToPrefab);
				}
			}

			// Setup GUI
			GUI.contentColor = Color.white;
		}

		/// <summary>
		/// <para>Dibuja el boton de layer</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawLayer(Rect rect)// Dibuja el boton de layer
		{
			// Setup GUI
			GUI.changed = false;

			// Dibujar
			EditorGUI.LabelField(rect, Styles.layerContent);
			var layer = EditorGUI.LayerField(rect, go.layer, Styles.layerStyle);

			// Si la GUI cambia
			if (GUI.changed) go.layer = layer;
		}

		/// <summary>
		/// <para>Dibuja el boton de tag</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawTag(Rect rect)// Dibuja el boton de tag
		{
			// Si la GUI cambia
			GUI.changed = false;

			EditorGUI.LabelField(rect, Styles.tagContent);
			var tag = EditorGUI.TagField(rect, Styles.tagContent, go.tag, Styles.tagStyle);

			// Si la GUI cambia
			if (GUI.changed) go.tag = tag;
		}

		/// <summary>
		/// <para>Dibuja los warnings</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawWarnings(Rect rect)// Dibuja los warnings
		{
			// Asignar variables
			var hasInfo = false;
			var hasWarning = false;
			var hasError = false;

			var contextEntries = (from entry in LogEntry.referencedObjects
								  where entry.Key && entry.Key == go.transform
								  select entry).ToArray();

			for (int i = 0; i < contextEntries.Length; i++)
			{
				if (!hasInfo && contextEntries[i].Value == (contextEntries[i].Value | EntradaModo.ScriptingLog))
					hasInfo = true;
				if (!hasWarning && contextEntries[i].Value == (contextEntries[i].Value | EntradaModo.ScriptingWarning))
					hasWarning = true;
				if (!hasError && contextEntries[i].Value == (contextEntries[i].Value | EntradaModo.ScriptingError))
					hasError = true;
			}

			if (!hasWarning)
			{
				var components = go.GetComponents<MonoBehaviour>();
				for (int i = 0; i < components.Length; i++)
					if (!components[i])
					{
						hasWarning = true;
						break;
					}
			}

			var icons = 0;
			if (hasInfo)
				icons++;
			if (hasWarning)
				icons++;
			if (hasError)
				icons++;

			var size = EditorStyles.label.CalcSize(new GUIContent(go.name)).x;

			if (Prefs.trailing)
				DoTaponado(rect, icons);

			rect.xMin += size;
			rect.xMin = Math.Min(rect.xMax - (Prefs.drawOrder.Count + icons) * rect.height - CalcMiniLabelSize().x - 5f - Prefs.offset, rect.xMin);
			rect.height = 17f;
			rect.xMax = rect.xMin + rect.height;

			if (hasInfo)
			{
				GUI.DrawTexture(rect, Styles.infoIcon, ScaleMode.ScaleToFit);
				rect.x += rect.width;
			}
			if (hasWarning)
			{
				GUI.DrawTexture(rect, Styles.warningIcon, ScaleMode.ScaleToFit);
				rect.x += rect.width;
			}
			if (hasError)
			{
				GUI.DrawTexture(rect, Styles.errorIcon, ScaleMode.ScaleToFit);
				rect.x += rect.width;
			}
		}

		/// <summary>
		/// <para>Dibuja los labels</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawMiniLabel(ref Rect rect)// Dibuja los labels
		{
			rect.x -= rect.height + 4f;
			rect.xMin += 15f;

			GUI.contentColor = currentColor;
			GUI.changed = false;

			var tag = string.Empty;
			var layer = 0;

			switch (Prefs.labelType)
			{
				case TipoMiniLabel.Tag:
					if (hasTag)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag)).x;
						tag = EditorGUI.TagField(rect, go.tag, Styles.miniLabelStyle);
						if (GUI.changed)
							go.tag = tag;
					}
					break;

				case TipoMiniLabel.Layer:
					if (hasLayer)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer))).x;
						layer = EditorGUI.LayerField(rect, go.layer, Styles.miniLabelStyle);
						if (GUI.changed)
							go.layer = layer;
					}
					break;

				case TipoMiniLabel.LayerOTag:
					if (hasLayer)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer))).x;
						layer = EditorGUI.LayerField(rect, go.layer, Styles.miniLabelStyle);
						if (GUI.changed)
							go.layer = layer;
					}
					else if (hasTag)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag)).x;
						tag = EditorGUI.TagField(rect, go.tag, Styles.miniLabelStyle);
						if (GUI.changed)
							go.tag = tag;
					}
					break;

				case TipoMiniLabel.TagOLayer:
					if (hasTag)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag)).x;
						tag = EditorGUI.TagField(rect, go.tag, Styles.miniLabelStyle);
						if (GUI.changed)
							go.tag = tag;
					}
					else if (hasLayer)
					{
						rect.xMin -= Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer))).x;
						layer = EditorGUI.LayerField(rect, go.layer, Styles.miniLabelStyle);
						if (GUI.changed)
							go.layer = layer;
					}
					break;
			}

			GUI.contentColor = Color.white;
		}

		/// <summary>
		/// <para>Dibuja el tooltip</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawTooltip(ref Rect rect)// Dibuja el tooltip
		{
			if (dragSelection != null)
				return;

			rect.xMax = rect.xMin;
			rect.xMin = 0f;

			var content = new GUIContent() { tooltip = string.Format("{0}\nTag: {1}\nLayer: {2}", go.name, go.tag, LayerMask.LayerToName(go.layer)) };
			EditorGUI.LabelField(rect, content);
		}

		/// <summary>
		/// <para>Dibuja el arbol</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawTree(Rect rect)// Dibuja el arbol
		{
			rect.xMin -= 14f;
			rect.xMax = rect.xMin + 14f;

			GUI.color = currentColor;

			if (go.transform.childCount == 0 && go.transform.parent)
			{
				if (Utility.LastInHierarchy(go.transform))
					GUI.DrawTexture(rect, Styles.treeEndTexture);
				else
					GUI.DrawTexture(rect, Styles.treeMiddleTexture);
			}

			var parent = go.transform.parent;

			for (rect.x -= 14f; rect.xMin > 0f && parent && parent.parent; rect.x -= 14f)
			{
				GUI.color = parent.parent.GetHierarchyColor();
				if (!parent.LastInHierarchy())
					GUI.DrawTexture(rect, Styles.treeLineTexture);
				parent = parent.parent;
			}

			GUI.color = Color.white;
		}

		/// <summary>
		/// <para>Dibuja el separador horizontal</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawHorizontalSeparador(Rect rect)// Dibuja el separador horizontal
		{
			rect.xMin = 0f;
			rect.yMax = rect.yMin + 1f;

			if (isFirstVisible)
			{
				var count = lastRect.y / lastRect.height;

				if (!Prefs.objectOnlySeparator)
					count = Math.Max(100, count);

				for (int i = 0; i < count; i++)
				{
					rect.y += lastRect.height;
					EditorGUI.DrawRect(rect, Styles.lineColor);
				}
			}
			else
				EditorGUI.DrawRect(rect, Styles.lineColor);
		}

		/// <summary>
		/// <para>Dibuja el separador vertical</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void DrawVerticalSeparador(Rect rect)// Dibuja el separador vertical
		{
			rect.xMax = rect.xMin + 1f;
			rect.yMax = Prefs.objectOnlySeparator ? rect.yMax : Mathf.Max(10000f, rect.yMax);
			EditorGUI.DrawRect(rect, Styles.lineColor);
		}

		/// <summary>
		/// <para>Color</para>
		/// </summary>
		/// <param name="rect">Rectangulo</param>
		private static void ColorSort(Rect rect)// Color
		{
			rect.xMin = 0f;

			var count = lastRect.y / lastRect.height;

			if (!Prefs.objectOnlySeparator)
				count = Math.Max(100, count);

			for (int i = 0; i < count; i++)
			{
				if ((rect.y / rect.height) % 2 < 1f)
					EditorGUI.DrawRect(rect, Styles.sortColor);
				rect.y += rect.height;
			}
		}

		/// <summary>
		/// <para>Seleccion del rectangulo</para>
		/// </summary>
		/// <param name="currentRect">Actual rectangulo</param>
		private static void DoSeleccion(Rect currentRect)// Seleccion del rectangulo
		{
			currentRect.xMin = 0f;

			if (evt.button == 1 && isFirstVisible)
			{
				switch (evt.type)
				{
					case EventType.MouseDrag:
						if (dragSelection == null)
						{
							dragSelection = new List<GameObject>();
							selectionStart = evt.mousePosition;
							selectionRect = new Rect();
						}

						selectionRect = new Rect();
						selectionRect.xMin = Mathf.Min(evt.mousePosition.x, selectionStart.x);
						selectionRect.yMin = Mathf.Min(evt.mousePosition.y, selectionStart.y);
						selectionRect.xMax = Mathf.Max(evt.mousePosition.x, selectionStart.x);
						selectionRect.yMax = Mathf.Max(evt.mousePosition.y, selectionStart.y);

						if (evt.control)
							dragSelection.AddRange(Selection.gameObjects);

						Selection.objects = dragSelection.ToArray();

						evt.Use();
						break;

					case EventType.MouseUp:
						if (dragSelection != null)
						{
							dragSelection = null;
							evt.Use();
						}
						break;
				}
			}

			if (dragSelection != null)
			{
				if (dragSelection.Contains(go) && !selectionRect.Overlaps(currentRect))
					dragSelection.Remove(go);
				else if (!dragSelection.Contains(go) && selectionRect.Overlaps(currentRect))
					dragSelection.Add(go);
			}
		}

		/// <summary>
		/// <para>Mostrar ... cuando el nombre sea grande</para>
		/// </summary>
		/// <param name="rect"></param>
		/// <param name="icons"></param>
		private static void DoTaponado(Rect rect, int icons = 0)// Mostrar ... cuando el nombre sea grande
		{
			var size = Styles.labelNormal.CalcSize(new GUIContent(go.name));
			var odd = (rect.y / rect.height) % 2 < 1f;

			rect.xMax -= (Prefs.drawOrder.Count + icons) * rect.height + CalcMiniLabelSize().x + Prefs.offset;

			if (size.x < rect.width)
				return;

			rect.yMin += 2f;
			rect.xMin = rect.xMax - 18f;
			rect.xMax = 1000f;

			EditorGUI.DrawRect(rect, Styles.normalColor * Prefs.PlaymodeTint);
			if (odd && Prefs.colorSeparator)
				EditorGUI.DrawRect(rect, Styles.sortColor * Prefs.PlaymodeTint);
			if (Selection.gameObjects.Contains(go))
				EditorGUI.DrawRect(rect, Styles.selectedColor);

			GUI.contentColor = currentColor;
			EditorGUI.LabelField(rect, "...");
			GUI.contentColor = Color.white;

			return;
		}
		#endregion

		#region Funcionalidad
		/// <summary>
		/// <para>Calcula la longitud del label</para>
		/// </summary>
		/// <returns></returns>
		private static Vector2 CalcMiniLabelSize()// Calcula la longitud del label
		{
			switch (Prefs.labelType)
			{
				case TipoMiniLabel.Tag:
					if (hasTag)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag));
					break;

				case TipoMiniLabel.Layer:
					if (hasLayer)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer)));
					break;

				case TipoMiniLabel.LayerOTag:
					if (hasLayer)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer)));
					else if (hasTag)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag));
					break;

				case TipoMiniLabel.TagOLayer:
					if (hasTag)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(go.tag));
					else if (hasLayer)
						return Styles.miniLabelStyle.CalcSize(new GUIContent(LayerMask.LayerToName(go.layer)));
					break;
			}

			return Vector2.zero;
		}
		#endregion
	}

	/// <summary>
	/// <para>Todos los estilos, iconos, colores y contenidos utilizados en la jerarquía</para>
	/// </summary>
	internal static class Styles
	{
		#region Styles
		static Styles()
		{
			if (EditorGUIUtility.isProSkin)
			{
				sortColor = new Color(0, 0, 0, 0.10f);
				lineColor = new Color32(30, 30, 30, 255);
				backgroundColorEnabled = new Color32(155, 155, 155, 255);
				backgroundColorDisabled = new Color32(155, 155, 155, 100);
				normalColor = new Color32(56, 56, 56, 255);
				selectedColor = new Color32(62, 95, 150, 255);
			}
			else
			{
				sortColor = new Color(1, 1, 1, 0.20f);
				lineColor = new Color32(100, 100, 100, 255);
				backgroundColorEnabled = new Color32(65, 65, 65, 255);
				backgroundColorDisabled = new Color32(65, 65, 65, 120);
				normalColor = new Color32(194, 194, 194, 255);
				selectedColor = new Color32(62, 125, 231, 255);
			}

			var offTexture = Utility.FindOrLoad(lockOff, "HierarchyLockOff");
			var onTexture = Utility.FindOrLoad(lockOn, "HierarchyLockOn");
			lockToggleStyle = Utility.CreateStyleFromTextures(onTexture, offTexture);

			offTexture = Utility.FindOrLoad(activeOff, "HierarchyActiveOff");
			onTexture = Utility.FindOrLoad(activeOn, "HierarchyActiveOn");
			activeToggleStyle = Utility.CreateStyleFromTextures(onTexture, offTexture);

			offTexture = Utility.FindOrLoad(staticOff, "HierarchyStaticOff");
			onTexture = Utility.FindOrLoad(staticOn, "HierarchyStaticOn");
			staticToggleStyle = Utility.CreateStyleFromTextures(onTexture, offTexture);

			onTexture = Utility.FindOrLoad(tag, "HierarchyTag");
			tagStyle = Utility.CreateStyleFromTextures(onTexture, onTexture);
			tagStyle.padding = new RectOffset(5, 17, 0, 1);
			tagStyle.border = new RectOffset();

			onTexture = Utility.FindOrLoad(layers, "HierarchyLayer");
			layerStyle = Utility.CreateStyleFromTextures(onTexture, onTexture);
			layerStyle.padding = new RectOffset(5, 17, 0, 1);
			layerStyle.border = new RectOffset();

			treeLineTexture = Utility.FindOrLoad(treeLine, "HierarchyTreeLine");
			treeMiddleTexture = Utility.FindOrLoad(treeMiddle, "HierarchyTreeMiddle");
			treeEndTexture = Utility.FindOrLoad(treeEnd, "HierarchyTreeEnd");

			infoIcon = Utility.FindOrLoad(info, "HierarchyInfo");
			warningIcon = Utility.FindOrLoad(warning, "HierarchyWarning");
			errorIcon = Utility.FindOrLoad(error, "HierarchyError");

			labelNormal = new GUIStyle("PR Label");
			labelDisabled = new GUIStyle("PR DisabledLabel");
			labelPrefab = new GUIStyle("PR PrefabLabel");
			labelPrefabDisabled = new GUIStyle("PR DisabledPrefabLabel");
			labelPrefabBroken = new GUIStyle("PR BrokenPrefabLabel");
			labelPrefabBrokenDisabled = new GUIStyle("PR DisabledBrokenPrefabLabel");

			miniLabelStyle = new GUIStyle("ShurikenLabel");
			miniLabelStyle.alignment = TextAnchor.MiddleRight;
			miniLabelStyle.clipping = TextClipping.Overflow;
			miniLabelStyle.normal.textColor = Color.white;
			miniLabelStyle.hover.textColor = Color.white;
			miniLabelStyle.focused.textColor = Color.white;
			miniLabelStyle.active.textColor = Color.white;

			applyPrefabStyle = new GUIStyle("ShurikenLabel");
			applyPrefabStyle.alignment = TextAnchor.MiddleCenter;
			applyPrefabStyle.clipping = TextClipping.Overflow;
			applyPrefabStyle.normal.textColor = Color.white;
			applyPrefabStyle.hover.textColor = Color.white;
			applyPrefabStyle.focused.textColor = Color.white;
			applyPrefabStyle.active.textColor = Color.white;

			ReloadTooltips();
		}
		#endregion

		#region Metodos
		public static void ReloadTooltips()
		{
			prefabApplyContent = new GUIContent("A");
			staticContent = new GUIContent();
			lockContent = new GUIContent();
			activeContent = new GUIContent();
			tagContent = new GUIContent();
			layerContent = new GUIContent();

			if (Prefs.tooltips)
			{
				prefabApplyContent.tooltip = "Aplicar Cambios Prefab";
				staticContent.tooltip = "Estatico";
				lockContent.tooltip = "Bloqueado/Desbloqueado";
				activeContent.tooltip = "Activado/Desactivado";
				tagContent.tooltip = "Tag";
				layerContent.tooltip = "Layer";
			}
		}
		#endregion

		#region Texturas Source
		private static readonly byte[] warning = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 2, 135, 73, 68, 65, 84, 88, 9, 99, 180, 176, 176, 96, 160, 0, 200, 1, 245, 62, 162, 64, 63, 3, 19, 37, 154, 129, 122, 159, 80, 168, 159, 98, 7, 252, 27, 104, 7, 80, 106, 63, 197, 33, 160, 15, 116, 1, 15, 37, 174, 160, 36, 13, 112, 0, 45, 158, 0, 196, 121, 3, 229, 0, 23, 19, 85, 54, 7, 11, 13, 246, 98, 160, 3, 140, 201, 117, 4, 11, 153, 26, 185, 89, 152, 25, 74, 18, 221, 120, 65, 218, 133, 206, 220, 254, 217, 251, 231, 47, 131, 3, 57, 102, 145, 27, 5, 249, 222, 102, 92, 246, 50, 162, 44, 12, 32, 12, 98, 3, 45, 167, 155, 3, 140, 4, 184, 153, 242, 194, 237, 185, 225, 30, 6, 177, 133, 120, 153, 90, 128, 2, 36, 39, 72, 114, 66, 160, 55, 218, 137, 91, 156, 155, 3, 161, 21, 196, 142, 116, 224, 182, 6, 58, 128, 228, 4, 137, 48, 5, 238, 31, 188, 12, 7, 37, 9, 22, 7, 23, 67, 78, 12, 69, 32, 49, 21, 41, 150, 66, 160, 132, 17, 134, 36, 30, 1, 82, 28, 0, 10, 243, 250, 20, 79, 94, 6, 38, 38, 70, 12, 35, 65, 98, 73, 238, 188, 34, 64, 137, 94, 12, 73, 60, 2, 164, 56, 32, 223, 70, 155, 221, 65, 91, 158, 13, 167, 113, 32, 57, 144, 26, 160, 2, 16, 38, 10, 16, 235, 0, 99, 118, 86, 134, 220, 4, 87, 112, 182, 131, 27, 236, 223, 240, 146, 1, 132, 145, 1, 72, 13, 39, 27, 99, 19, 80, 140, 168, 4, 73, 172, 3, 122, 2, 173, 184, 37, 68, 5, 152, 145, 237, 194, 202, 6, 169, 241, 183, 228, 178, 5, 74, 18, 149, 32, 137, 113, 128, 131, 8, 63, 147, 67, 144, 53, 34, 219, 97, 181, 25, 73, 16, 164, 86, 76, 128, 41, 31, 40, 100, 136, 36, 140, 149, 73, 140, 3, 234, 65, 193, 202, 206, 134, 153, 240, 176, 154, 8, 20, 4, 169, 141, 115, 225, 21, 3, 50, 251, 112, 169, 129, 137, 19, 42, 138, 29, 180, 228, 88, 29, 128, 9, 11, 166, 30, 133, 222, 216, 32, 142, 194, 71, 230, 128, 244, 108, 59, 197, 234, 112, 237, 209, 111, 59, 160, 248, 33, 100, 57, 100, 54, 190, 16, 224, 102, 100, 100, 104, 0, 101, 59, 70, 32, 131, 84, 0, 210, 3, 209, 203, 208, 4, 212, 139, 51, 254, 240, 57, 32, 31, 88, 184, 216, 43, 75, 178, 226, 180, 27, 91, 46, 64, 86, 12, 210, 11, 50, 3, 40, 6, 74, 15, 88, 1, 46, 7, 24, 115, 115, 48, 230, 198, 58, 19, 149, 147, 176, 26, 12, 19, 4, 153, 1, 50, 11, 200, 199, 90, 101, 51, 203, 200, 200, 192, 212, 34, 211, 203, 98, 156, 120, 116, 12, 148, 177, 199, 61, 76, 97, 164, 3, 15, 176, 14, 192, 239, 72, 14, 96, 130, 100, 102, 98, 224, 185, 112, 247, 151, 26, 80, 223, 66, 152, 94, 24, 141, 45, 4, 28, 164, 69, 152, 29, 124, 204, 185, 96, 106, 40, 166, 65, 102, 129, 204, 4, 26, 4, 74, 144, 40, 0, 221, 1, 224, 242, 30, 212, 208, 96, 97, 38, 61, 225, 161, 152, 140, 196, 1, 153, 5, 109, 188, 52, 2, 133, 81, 18, 36, 35, 176, 99, 2, 178, 233, 63, 84, 189, 19, 144, 14, 6, 98, 80, 145, 7, 194, 160, 102, 55, 72, 14, 132, 65, 234, 64, 113, 2, 202, 186, 132, 178, 47, 80, 9, 78, 176, 18, 40, 179, 1, 42, 203, 4, 50, 8, 102, 57, 72, 108, 31, 20, 131, 216, 244, 0, 255, 208, 163, 128, 30, 150, 162, 216, 49, 234, 128, 209, 16, 0, 0, 243, 227, 86, 28, 203, 210, 193, 221, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] info = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 2, 5, 73, 68, 65, 84, 88, 9, 237, 87, 187, 74, 3, 65, 20, 157, 53, 10, 10, 162, 130, 154, 52, 177, 176, 244, 15, 178, 95, 96, 167, 63, 96, 99, 45, 182, 38, 191, 144, 94, 180, 73, 126, 195, 206, 66, 16, 22, 108, 69, 20, 27, 75, 73, 16, 180, 16, 124, 69, 227, 57, 235, 206, 50, 187, 115, 209, 205, 44, 195, 54, 94, 56, 153, 201, 157, 123, 239, 57, 243, 216, 205, 36, 104, 181, 90, 170, 74, 155, 170, 146, 156, 220, 255, 2, 170, 88, 129, 192, 220, 246, 105, 243, 11, 250, 28, 156, 5, 66, 96, 13, 48, 141, 177, 95, 9, 180, 159, 19, 32, 70, 218, 145, 180, 249, 216, 87, 248, 35, 224, 30, 32, 199, 59, 16, 91, 94, 0, 157, 203, 192, 65, 24, 134, 155, 237, 118, 91, 53, 26, 141, 56, 208, 245, 99, 48, 24, 168, 110, 183, 171, 162, 40, 58, 100, 93, 224, 195, 172, 37, 109, 193, 12, 2, 106, 157, 78, 167, 52, 57, 137, 56, 1, 214, 50, 108, 108, 244, 197, 167, 128, 203, 172, 234, 245, 186, 25, 87, 170, 111, 212, 226, 242, 255, 41, 64, 90, 149, 82, 2, 140, 228, 12, 57, 253, 18, 153, 21, 100, 20, 200, 116, 113, 78, 20, 49, 129, 101, 158, 0, 230, 149, 18, 48, 1, 177, 14, 181, 38, 39, 9, 176, 84, 234, 108, 31, 173, 36, 192, 82, 233, 131, 88, 215, 148, 222, 3, 133, 87, 0, 207, 182, 174, 227, 220, 74, 43, 224, 92, 204, 37, 81, 18, 80, 120, 11, 28, 158, 2, 75, 163, 36, 192, 10, 242, 233, 144, 206, 64, 97, 81, 190, 206, 64, 252, 42, 246, 52, 107, 235, 128, 75, 179, 149, 124, 165, 244, 12, 135, 67, 157, 111, 157, 47, 105, 11, 172, 32, 157, 237, 210, 234, 159, 99, 228, 242, 206, 96, 213, 46, 36, 160, 215, 235, 169, 126, 191, 127, 135, 2, 251, 192, 6, 192, 75, 203, 164, 118, 134, 4, 222, 5, 50, 34, 36, 1, 153, 125, 74, 200, 31, 145, 184, 13, 92, 2, 39, 128, 171, 113, 123, 89, 63, 21, 33, 237, 119, 58, 136, 89, 143, 129, 91, 36, 236, 0, 87, 192, 34, 48, 15, 196, 151, 22, 180, 186, 96, 13, 125, 66, 170, 71, 66, 142, 45, 0, 214, 37, 163, 214, 108, 54, 225, 79, 141, 193, 75, 192, 86, 16, 4, 235, 152, 253, 5, 250, 187, 192, 57, 192, 49, 222, 237, 120, 159, 251, 4, 248, 180, 80, 44, 161, 239, 138, 169, 120, 248, 180, 233, 241, 55, 56, 158, 129, 76, 76, 144, 251, 103, 68, 146, 85, 224, 8, 88, 1, 246, 128, 27, 128, 132, 94, 44, 127, 6, 168, 238, 1, 56, 6, 158, 128, 107, 192, 231, 123, 65, 229, 5, 128, 47, 38, 60, 69, 59, 231, 155, 156, 100, 210, 161, 161, 159, 246, 242, 211, 248, 253, 252, 77, 128, 95, 230, 164, 122, 229, 2, 190, 1, 44, 221, 109, 142, 7, 54, 64, 137, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] layers = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 174, 73, 68, 65, 84, 88, 9, 237, 149, 189, 74, 196, 64, 20, 133, 205, 170, 176, 130, 88, 40, 86, 54, 218, 219, 248, 131, 181, 173, 173, 157, 165, 8, 226, 3, 216, 175, 22, 62, 128, 253, 194, 250, 18, 138, 96, 109, 183, 98, 107, 165, 216, 89, 8, 138, 22, 10, 34, 241, 59, 50, 3, 33, 235, 205, 206, 76, 192, 109, 114, 224, 99, 38, 115, 239, 185, 119, 54, 155, 73, 178, 60, 207, 199, 70, 169, 214, 40, 155, 171, 119, 179, 129, 230, 14, 212, 185, 3, 109, 158, 161, 174, 67, 243, 52, 233, 24, 38, 176, 132, 167, 15, 94, 154, 107, 45, 186, 86, 180, 129, 38, 91, 240, 12, 101, 105, 77, 177, 168, 154, 49, 201, 45, 138, 119, 224, 27, 44, 41, 214, 1, 229, 6, 213, 14, 74, 162, 216, 28, 92, 64, 168, 148, 43, 207, 208, 250, 67, 19, 40, 178, 10, 247, 16, 43, 121, 228, 173, 236, 81, 25, 196, 188, 7, 31, 144, 42, 121, 85, 195, 236, 83, 231, 24, 134, 30, 187, 234, 175, 93, 213, 238, 92, 108, 141, 241, 1, 98, 165, 191, 96, 5, 204, 95, 175, 88, 101, 176, 96, 142, 125, 8, 207, 241, 206, 22, 252, 102, 31, 51, 80, 48, 79, 187, 185, 142, 214, 17, 196, 28, 67, 239, 53, 251, 152, 1, 215, 116, 155, 241, 5, 142, 193, 159, 237, 144, 23, 209, 56, 249, 39, 240, 10, 170, 97, 246, 49, 3, 206, 116, 200, 248, 5, 210, 37, 248, 179, 173, 215, 238, 141, 22, 157, 250, 140, 139, 160, 122, 243, 112, 5, 146, 188, 170, 97, 246, 49, 3, 5, 211, 38, 243, 39, 144, 30, 97, 29, 228, 107, 67, 215, 161, 185, 214, 54, 64, 57, 146, 60, 242, 106, 221, 196, 12, 148, 76, 11, 92, 95, 131, 244, 9, 251, 80, 246, 30, 184, 24, 195, 111, 174, 60, 229, 156, 129, 235, 129, 133, 10, 211, 36, 177, 83, 240, 234, 49, 153, 114, 156, 249, 69, 70, 229, 40, 55, 168, 118, 80, 82, 169, 216, 14, 215, 239, 32, 221, 58, 52, 215, 154, 98, 81, 53, 163, 146, 11, 197, 151, 153, 223, 129, 151, 230, 90, 139, 174, 151, 201, 148, 168, 25, 124, 61, 231, 221, 101, 124, 75, 169, 83, 103, 3, 234, 151, 185, 166, 201, 191, 98, 34, 101, 215, 5, 79, 114, 99, 95, 227, 63, 190, 134, 190, 215, 159, 99, 179, 129, 230, 14, 252, 0, 225, 160, 157, 11, 42, 215, 224, 160, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] staticOff = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 70, 73, 68, 65, 84, 88, 9, 237, 86, 49, 14, 194, 48, 12, 164, 8, 9, 30, 196, 31, 40, 27, 60, 1, 102, 126, 192, 194, 7, 16, 3, 15, 97, 230, 65, 140, 93, 105, 39, 194, 185, 212, 81, 154, 54, 73, 77, 131, 42, 161, 90, 178, 234, 216, 78, 238, 226, 196, 109, 19, 165, 212, 100, 72, 153, 14, 9, 78, 216, 35, 129, 177, 2, 127, 89, 129, 20, 151, 59, 131, 94, 160, 9, 212, 47, 244, 30, 136, 168, 107, 172, 85, 64, 89, 174, 48, 18, 168, 19, 35, 246, 17, 44, 177, 221, 185, 177, 229, 3, 236, 179, 49, 110, 154, 62, 118, 130, 24, 237, 124, 87, 229, 159, 240, 52, 37, 195, 192, 89, 1, 103, 192, 55, 201, 138, 113, 217, 95, 240, 239, 171, 24, 147, 200, 49, 94, 89, 249, 53, 204, 218, 192, 151, 232, 136, 49, 56, 194, 165, 152, 36, 142, 240, 120, 193, 17, 159, 244, 33, 96, 131, 127, 40, 40, 69, 36, 248, 56, 130, 235, 127, 123, 9, 169, 213, 110, 80, 243, 194, 241, 5, 43, 96, 60, 120, 16, 124, 82, 25, 132, 154, 34, 223, 108, 53, 12, 181, 60, 97, 5, 203, 142, 28, 141, 169, 13, 211, 233, 177, 163, 130, 19, 142, 132, 64, 116, 112, 9, 1, 2, 167, 150, 106, 19, 113, 217, 177, 136, 222, 184, 54, 76, 167, 101, 251, 192, 131, 125, 110, 173, 213, 192, 107, 56, 172, 9, 63, 5, 39, 172, 242, 67, 225, 105, 149, 28, 177, 69, 75, 156, 90, 109, 3, 189, 183, 196, 68, 174, 16, 129, 182, 95, 230, 104, 224, 196, 84, 250, 34, 138, 10, 46, 37, 64, 224, 91, 104, 239, 178, 19, 48, 203, 140, 13, 199, 51, 252, 71, 227, 152, 216, 213, 45, 61, 130, 174, 235, 118, 206, 27, 9, 140, 21, 24, 188, 2, 111, 34, 60, 124, 38, 68, 169, 12, 151, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] treeLine = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 16, 0, 0, 0, 16, 8, 6, 0, 0, 0, 31, 243, 255, 97, 0, 0, 0, 40, 73, 68, 65, 84, 56, 17, 99, 252, 255, 255, 63, 3, 17, 0, 164, 136, 17, 155, 58, 38, 108, 130, 164, 136, 141, 26, 192, 192, 48, 26, 6, 163, 97, 0, 202, 51, 3, 159, 14, 0, 75, 210, 4, 29, 114, 117, 87, 211, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] lockOn = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 42, 73, 68, 65, 84, 88, 9, 237, 83, 75, 10, 194, 48, 16, 109, 221, 235, 222, 162, 30, 72, 232, 57, 188, 129, 120, 0, 47, 225, 69, 92, 246, 48, 42, 186, 87, 55, 174, 234, 123, 37, 5, 137, 157, 233, 164, 17, 74, 165, 15, 30, 77, 243, 230, 151, 201, 36, 45, 203, 50, 233, 19, 147, 62, 147, 51, 247, 88, 192, 96, 59, 144, 227, 250, 10, 240, 233, 200, 53, 247, 194, 193, 87, 16, 200, 61, 236, 37, 80, 11, 138, 23, 100, 140, 224, 185, 203, 252, 194, 119, 11, 102, 142, 92, 115, 143, 160, 141, 57, 174, 217, 208, 5, 45, 152, 1, 216, 129, 190, 47, 247, 8, 218, 248, 154, 248, 159, 210, 56, 0, 119, 216, 78, 193, 12, 188, 121, 126, 115, 252, 95, 193, 7, 56, 243, 52, 241, 55, 180, 128, 186, 218, 84, 136, 216, 166, 127, 185, 13, 226, 25, 174, 81, 246, 9, 172, 79, 199, 83, 112, 221, 68, 106, 4, 53, 250, 208, 87, 133, 229, 10, 206, 136, 176, 80, 163, 200, 226, 5, 210, 82, 150, 147, 196, 82, 192, 231, 201, 181, 88, 146, 38, 205, 75, 101, 63, 136, 25, 144, 78, 246, 147, 253, 152, 14, 28, 81, 193, 202, 145, 235, 78, 136, 153, 1, 14, 23, 135, 140, 224, 144, 114, 88, 155, 240, 191, 51, 112, 192, 113, 217, 5, 146, 235, 78, 136, 185, 2, 107, 194, 255, 189, 2, 107, 7, 84, 59, 203, 51, 172, 39, 93, 13, 36, 136, 173, 190, 150, 2, 54, 8, 46, 61, 49, 33, 111, 181, 77, 31, 250, 170, 176, 12, 161, 26, 32, 86, 180, 116, 32, 54, 135, 234, 63, 22, 48, 118, 224, 13, 132, 45, 106, 222, 94, 69, 33, 212, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] treeMiddle = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 16, 0, 0, 0, 16, 8, 6, 0, 0, 0, 31, 243, 255, 97, 0, 0, 0, 50, 73, 68, 65, 84, 56, 17, 99, 252, 255, 255, 63, 3, 17, 0, 164, 136, 17, 155, 58, 38, 108, 130, 164, 136, 141, 26, 192, 192, 48, 240, 97, 192, 66, 66, 148, 97, 77, 48, 164, 24, 48, 154, 144, 112, 132, 246, 192, 167, 3, 0, 47, 77, 5, 33, 173, 88, 115, 47, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] tag = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 0, 245, 73, 68, 65, 84, 88, 9, 237, 148, 49, 14, 194, 48, 12, 69, 41, 140, 92, 130, 165, 18, 3, 87, 96, 64, 176, 208, 129, 3, 114, 19, 196, 196, 61, 24, 162, 194, 12, 19, 11, 18, 106, 255, 239, 144, 133, 212, 73, 6, 19, 6, 127, 233, 73, 109, 237, 196, 191, 86, 226, 170, 235, 186, 73, 73, 77, 75, 22, 103, 109, 51, 96, 29, 176, 14, 252, 125, 7, 26, 92, 213, 22, 112, 90, 165, 224, 144, 87, 131, 116, 113, 18, 10, 220, 16, 203, 213, 3, 11, 214, 64, 218, 215, 199, 42, 38, 10, 18, 131, 194, 186, 55, 98, 7, 112, 18, 114, 134, 144, 150, 1, 110, 78, 243, 27, 112, 225, 203, 152, 52, 13, 176, 230, 7, 44, 193, 149, 47, 33, 105, 223, 130, 25, 138, 158, 193, 60, 84, 156, 223, 180, 13, 176, 198, 2, 28, 249, 16, 210, 47, 12, 132, 234, 250, 111, 218, 103, 128, 133, 28, 88, 129, 23, 248, 146, 182, 129, 162, 135, 144, 215, 112, 11, 70, 111, 0, 219, 161, 117, 6, 56, 136, 246, 64, 156, 1, 41, 6, 238, 76, 202, 212, 19, 249, 59, 16, 157, 130, 195, 190, 145, 153, 221, 32, 222, 130, 84, 57, 36, 214, 192, 207, 250, 216, 115, 236, 16, 102, 254, 124, 126, 186, 214, 25, 72, 118, 98, 6, 172, 3, 214, 129, 226, 29, 232, 1, 225, 73, 18, 53, 46, 147, 94, 90, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] activeOff = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 2, 16, 73, 68, 65, 84, 88, 9, 237, 150, 191, 46, 68, 81, 16, 198, 45, 235, 79, 86, 4, 9, 79, 160, 148, 16, 15, 64, 130, 168, 121, 26, 141, 70, 179, 17, 5, 141, 74, 34, 161, 82, 208, 136, 146, 70, 104, 116, 66, 36, 158, 64, 65, 132, 206, 159, 4, 235, 247, 37, 231, 108, 206, 158, 59, 231, 102, 237, 102, 67, 177, 147, 124, 57, 247, 124, 51, 103, 102, 118, 238, 156, 185, 91, 168, 84, 42, 29, 127, 41, 157, 127, 25, 92, 177, 219, 9, 180, 43, 240, 175, 43, 176, 64, 147, 30, 129, 146, 186, 181, 85, 82, 200, 153, 3, 10, 190, 8, 78, 193, 18, 120, 5, 18, 85, 109, 10, 140, 131, 81, 183, 127, 98, 189, 1, 215, 224, 19, 212, 47, 74, 32, 129, 18, 252, 9, 144, 104, 213, 94, 182, 219, 32, 37, 47, 40, 182, 192, 24, 72, 249, 173, 225, 107, 54, 198, 33, 43, 137, 110, 236, 14, 65, 158, 124, 160, 44, 3, 217, 230, 198, 200, 85, 114, 120, 24, 156, 3, 47, 190, 18, 69, 136, 105, 208, 239, 48, 203, 122, 12, 98, 57, 131, 24, 2, 201, 56, 73, 5, 135, 244, 235, 47, 65, 44, 62, 9, 235, 236, 74, 108, 204, 94, 62, 252, 235, 203, 156, 201, 16, 24, 123, 110, 207, 112, 246, 230, 56, 159, 68, 23, 251, 77, 176, 14, 122, 128, 206, 90, 149, 216, 113, 58, 239, 187, 186, 86, 31, 34, 131, 5, 246, 150, 44, 65, 42, 184, 36, 76, 98, 159, 253, 26, 144, 63, 189, 14, 75, 230, 32, 51, 241, 50, 132, 51, 186, 176, 60, 192, 13, 2, 149, 243, 221, 233, 195, 36, 54, 224, 228, 111, 192, 233, 226, 69, 189, 148, 137, 151, 154, 132, 169, 63, 9, 223, 92, 112, 205, 131, 103, 119, 209, 253, 176, 234, 101, 191, 236, 184, 212, 82, 48, 21, 86, 86, 112, 42, 151, 37, 42, 175, 126, 133, 202, 29, 138, 175, 132, 116, 51, 161, 34, 120, 158, 231, 57, 83, 129, 12, 17, 24, 169, 113, 98, 81, 131, 233, 140, 26, 78, 141, 119, 15, 172, 215, 113, 0, 31, 202, 46, 27, 51, 150, 73, 58, 227, 212, 53, 212, 85, 11, 207, 201, 206, 106, 76, 159, 68, 195, 215, 80, 65, 52, 68, 206, 64, 44, 170, 132, 94, 135, 6, 145, 26, 115, 17, 88, 87, 116, 21, 190, 225, 65, 228, 127, 165, 198, 105, 25, 104, 188, 214, 43, 97, 79, 120, 63, 230, 106, 146, 68, 177, 120, 125, 96, 244, 161, 209, 7, 39, 37, 15, 40, 110, 157, 178, 174, 36, 10, 24, 155, 183, 35, 135, 44, 162, 155, 4, 19, 96, 4, 124, 129, 71, 112, 7, 174, 64, 31, 208, 167, 92, 87, 52, 254, 148, 67, 69, 162, 4, 90, 128, 176, 49, 143, 242, 252, 183, 34, 184, 247, 169, 36, 20, 92, 99, 221, 115, 153, 181, 145, 87, 16, 213, 176, 185, 109, 106, 20, 55, 231, 245, 23, 167, 219, 9, 180, 43, 240, 3, 153, 228, 93, 20, 87, 153, 61, 168, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] treeEnd = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 16, 0, 0, 0, 16, 8, 6, 0, 0, 0, 31, 243, 255, 97, 0, 0, 0, 53, 73, 68, 65, 84, 56, 17, 99, 252, 255, 255, 63, 3, 17, 0, 164, 136, 17, 155, 58, 38, 108, 130, 164, 136, 141, 26, 192, 192, 48, 12, 194, 128, 133, 132, 56, 199, 154, 226, 136, 53, 0, 107, 42, 4, 89, 62, 12, 2, 113, 224, 189, 0, 0, 237, 62, 5, 33, 13, 93, 99, 58, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] staticOn = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 196, 73, 68, 65, 84, 88, 9, 237, 86, 59, 78, 195, 64, 16, 141, 145, 144, 76, 238, 195, 5, 210, 4, 110, 17, 74, 238, 64, 193, 17, 16, 5, 23, 160, 164, 11, 5, 223, 158, 211, 132, 22, 10, 36, 243, 158, 189, 179, 154, 221, 204, 142, 109, 20, 146, 198, 35, 141, 118, 126, 59, 239, 237, 248, 91, 53, 77, 51, 59, 164, 28, 29, 18, 156, 216, 19, 129, 105, 2, 255, 53, 129, 37, 238, 175, 13, 244, 22, 90, 65, 203, 194, 199, 112, 199, 186, 68, 191, 47, 168, 200, 29, 140, 10, 106, 226, 120, 19, 88, 128, 246, 26, 58, 47, 211, 55, 51, 167, 136, 214, 42, 115, 9, 251, 70, 249, 169, 89, 98, 134, 248, 35, 148, 242, 14, 157, 67, 205, 19, 168, 248, 25, 236, 85, 240, 175, 177, 106, 249, 132, 99, 238, 55, 131, 161, 152, 160, 111, 161, 203, 43, 86, 143, 4, 193, 57, 246, 31, 232, 5, 148, 125, 133, 196, 55, 108, 230, 77, 44, 51, 168, 138, 53, 137, 23, 196, 79, 84, 78, 246, 10, 56, 82, 173, 104, 18, 87, 136, 156, 67, 165, 118, 107, 221, 10, 24, 197, 36, 65, 112, 74, 78, 34, 7, 239, 170, 186, 73, 172, 224, 244, 246, 239, 45, 8, 77, 120, 114, 33, 241, 12, 187, 14, 241, 123, 172, 150, 240, 114, 20, 199, 142, 92, 196, 141, 134, 14, 22, 108, 146, 32, 56, 229, 9, 74, 18, 199, 208, 7, 168, 150, 193, 224, 216, 52, 27, 67, 128, 181, 4, 253, 8, 104, 22, 137, 81, 224, 127, 33, 192, 177, 18, 68, 68, 147, 224, 229, 24, 52, 118, 212, 197, 131, 71, 67, 7, 11, 118, 14, 142, 178, 86, 132, 196, 152, 94, 177, 54, 26, 104, 229, 217, 37, 240, 192, 33, 222, 19, 94, 15, 51, 103, 6, 51, 50, 30, 56, 47, 135, 220, 19, 188, 65, 173, 247, 132, 139, 225, 38, 3, 17, 125, 205, 17, 138, 194, 56, 201, 213, 80, 121, 58, 242, 247, 68, 111, 255, 222, 130, 8, 151, 26, 2, 46, 251, 245, 123, 130, 36, 188, 215, 182, 236, 105, 215, 196, 193, 70, 203, 79, 161, 187, 167, 128, 39, 207, 107, 53, 9, 126, 67, 6, 145, 200, 155, 88, 190, 38, 144, 159, 60, 175, 39, 40, 63, 92, 148, 65, 36, 242, 6, 187, 240, 73, 130, 159, 112, 10, 63, 233, 110, 79, 55, 217, 183, 217, 201, 147, 196, 26, 186, 112, 106, 90, 236, 246, 87, 41, 253, 69, 217, 175, 231, 253, 146, 237, 133, 201, 68, 96, 154, 192, 47, 215, 99, 63, 63, 69, 84, 53, 252, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] error = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 2, 62, 73, 68, 65, 84, 88, 9, 237, 87, 65, 107, 19, 65, 20, 158, 205, 118, 39, 73, 217, 129, 180, 61, 212, 67, 68, 130, 7, 65, 193, 72, 245, 144, 139, 160, 231, 130, 66, 47, 94, 148, 138, 98, 208, 131, 55, 241, 39, 136, 151, 30, 74, 78, 10, 254, 2, 197, 131, 208, 155, 23, 47, 254, 0, 5, 165, 20, 4, 9, 66, 74, 178, 18, 8, 72, 49, 147, 141, 239, 169, 67, 222, 76, 102, 102, 27, 66, 201, 101, 31, 60, 246, 237, 247, 190, 121, 223, 183, 203, 102, 134, 4, 141, 70, 131, 45, 50, 10, 139, 20, 71, 237, 220, 64, 254, 6, 150, 102, 252, 8, 107, 192, 127, 10, 89, 132, 44, 59, 214, 254, 2, 124, 7, 242, 139, 163, 175, 193, 62, 3, 33, 48, 71, 132, 125, 249, 18, 231, 175, 238, 196, 113, 157, 7, 1, 129, 39, 229, 239, 241, 152, 189, 28, 12, 216, 254, 112, 184, 2, 232, 45, 200, 225, 164, 107, 175, 194, 106, 181, 106, 239, 48, 54, 38, 141, 173, 243, 81, 244, 250, 174, 16, 53, 151, 56, 114, 67, 48, 6, 38, 89, 91, 202, 211, 189, 52, 61, 2, 232, 35, 153, 97, 45, 125, 6, 212, 130, 38, 136, 239, 222, 23, 98, 213, 39, 174, 200, 104, 162, 206, 57, 255, 33, 101, 189, 155, 166, 223, 0, 255, 170, 122, 182, 107, 150, 129, 235, 231, 162, 232, 205, 3, 33, 202, 199, 17, 87, 2, 104, 226, 34, 231, 113, 71, 202, 43, 135, 105, 154, 0, 254, 89, 245, 204, 171, 239, 103, 248, 28, 158, 252, 109, 83, 8, 230, 18, 127, 156, 36, 12, 211, 22, 184, 102, 91, 136, 179, 23, 162, 8, 63, 200, 27, 54, 14, 98, 62, 3, 103, 224, 181, 87, 92, 226, 174, 129, 20, 199, 181, 247, 132, 88, 7, 236, 38, 197, 105, 237, 51, 224, 124, 114, 58, 32, 171, 254, 255, 0, 203, 132, 167, 105, 106, 55, 132, 116, 146, 165, 246, 27, 94, 132, 1, 237, 225, 76, 3, 154, 59, 141, 121, 66, 55, 190, 157, 48, 83, 178, 181, 182, 150, 201, 201, 34, 152, 111, 32, 139, 175, 245, 15, 71, 35, 134, 57, 79, 120, 13, 224, 222, 238, 138, 14, 8, 63, 235, 247, 255, 38, 214, 174, 240, 205, 192, 53, 166, 1, 170, 184, 135, 7, 75, 214, 0, 151, 48, 226, 234, 112, 130, 82, 18, 158, 230, 214, 220, 138, 241, 4, 84, 38, 62, 37, 105, 202, 190, 75, 121, 13, 15, 24, 220, 94, 105, 196, 133, 2, 219, 40, 22, 217, 213, 82, 137, 157, 10, 113, 153, 30, 74, 28, 78, 70, 60, 150, 31, 65, 254, 212, 25, 255, 238, 76, 3, 40, 94, 129, 196, 147, 12, 227, 67, 150, 9, 52, 98, 6, 17, 127, 7, 189, 45, 200, 182, 201, 81, 247, 166, 1, 196, 149, 184, 226, 120, 77, 40, 146, 186, 18, 241, 23, 128, 61, 132, 236, 171, 158, 237, 106, 51, 96, 227, 29, 203, 4, 17, 127, 15, 67, 158, 64, 246, 108, 195, 40, 22, 204, 248, 199, 228, 54, 44, 222, 164, 3, 44, 117, 23, 176, 22, 228, 129, 165, 55, 5, 205, 106, 96, 106, 192, 188, 192, 244, 23, 52, 239, 196, 25, 215, 231, 6, 242, 55, 240, 7, 253, 247, 174, 203, 103, 130, 76, 163, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] lockOff = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 36, 73, 68, 65, 84, 88, 9, 237, 83, 187, 13, 194, 48, 16, 77, 232, 161, 39, 2, 6, 66, 202, 28, 108, 128, 24, 128, 37, 88, 132, 50, 195, 0, 130, 30, 104, 168, 194, 123, 200, 110, 44, 238, 114, 142, 139, 40, 145, 159, 244, 20, 199, 239, 126, 62, 159, 203, 182, 109, 139, 33, 49, 27, 50, 57, 115, 231, 2, 70, 219, 129, 26, 215, 215, 128, 111, 71, 174, 185, 23, 15, 190, 130, 72, 30, 97, 47, 129, 90, 84, 188, 40, 99, 4, 175, 93, 230, 15, 190, 123, 176, 114, 228, 154, 123, 4, 109, 204, 113, 205, 134, 46, 104, 195, 12, 192, 1, 12, 125, 185, 71, 208, 38, 212, 196, 255, 146, 198, 17, 120, 194, 118, 14, 86, 224, 35, 240, 91, 226, 255, 14, 190, 192, 69, 160, 137, 191, 177, 5, 136, 129, 156, 224, 79, 83, 118, 25, 122, 125, 20, 207, 112, 139, 106, 47, 32, 79, 215, 69, 127, 48, 218, 209, 135, 190, 42, 44, 87, 112, 69, 132, 149, 26, 69, 22, 111, 144, 214, 178, 92, 20, 150, 2, 252, 189, 106, 113, 52, 77, 157, 135, 81, 204, 128, 118, 186, 100, 45, 165, 3, 103, 100, 223, 56, 114, 221, 11, 41, 51, 192, 225, 226, 144, 17, 28, 82, 14, 235, 63, 76, 119, 6, 78, 56, 46, 187, 64, 114, 221, 11, 41, 87, 96, 77, 56, 221, 43, 176, 118, 64, 181, 179, 60, 67, 63, 233, 106, 32, 65, 236, 244, 181, 20, 176, 67, 112, 233, 137, 9, 121, 127, 219, 244, 161, 175, 10, 203, 16, 170, 1, 82, 69, 75, 7, 82, 115, 168, 254, 185, 128, 220, 129, 47, 78, 248, 107, 220, 42, 197, 235, 77, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		private static readonly byte[] activeOn = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 32, 0, 0, 0, 32, 8, 6, 0, 0, 0, 115, 122, 122, 244, 0, 0, 1, 74, 73, 68, 65, 84, 88, 9, 237, 149, 177, 74, 3, 65, 16, 134, 61, 81, 11, 69, 176, 240, 13, 210, 107, 227, 11, 36, 175, 149, 230, 154, 32, 105, 181, 77, 227, 43, 248, 4, 98, 101, 171, 239, 96, 170, 128, 117, 64, 56, 255, 47, 204, 66, 216, 155, 61, 54, 135, 176, 41, 118, 224, 231, 246, 254, 249, 103, 102, 111, 110, 111, 174, 233, 186, 238, 164, 164, 157, 150, 44, 78, 237, 186, 129, 218, 129, 218, 129, 179, 17, 159, 33, 49, 247, 194, 157, 112, 107, 241, 27, 93, 191, 132, 79, 225, 215, 184, 188, 11, 131, 40, 19, 19, 233, 158, 133, 31, 33, 101, 248, 208, 160, 205, 202, 155, 35, 58, 87, 178, 133, 176, 21, 114, 13, 45, 49, 196, 14, 214, 24, 116, 42, 248, 70, 120, 19, 98, 123, 21, 49, 21, 174, 12, 172, 225, 98, 35, 150, 28, 201, 58, 73, 135, 130, 46, 133, 15, 33, 182, 185, 136, 84, 28, 190, 216, 200, 65, 46, 55, 198, 37, 77, 188, 138, 51, 233, 158, 167, 36, 230, 66, 88, 10, 223, 6, 214, 112, 248, 188, 78, 144, 203, 173, 229, 146, 18, 207, 4, 207, 104, 53, 49, 143, 142, 19, 14, 31, 26, 207, 200, 217, 171, 215, 35, 76, 244, 238, 101, 16, 119, 109, 254, 181, 227, 135, 35, 31, 26, 207, 200, 217, 171, 119, 180, 147, 176, 77, 76, 145, 7, 227, 95, 28, 127, 224, 130, 38, 150, 180, 49, 177, 187, 247, 218, 98, 92, 209, 67, 200, 187, 42, 254, 25, 178, 137, 162, 131, 40, 156, 88, 198, 41, 99, 181, 200, 40, 14, 155, 224, 154, 251, 51, 122, 50, 237, 126, 108, 114, 221, 72, 236, 30, 206, 1, 242, 95, 127, 199, 99, 54, 48, 176, 183, 195, 93, 71, 59, 136, 14, 127, 148, 145, 17, 181, 3, 181, 3, 197, 59, 240, 7, 140, 239, 36, 185, 94, 67, 99, 235, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
		#endregion

		#region Variables
		public static GUIContent prefabApplyContent;
		public static GUIContent staticContent;
		public static GUIContent lockContent;
		public static GUIContent activeContent;
		public static GUIContent tagContent;
		public static GUIContent layerContent;

		public static readonly Color sortColor;
		public static readonly Color lineColor;
		public static readonly Color backgroundColorEnabled;
		public static readonly Color backgroundColorDisabled;
		public static readonly Color normalColor;
		public static readonly Color selectedColor;

		public static readonly GUIStyle staticToggleStyle;
		public static readonly GUIStyle applyPrefabStyle;
		public static readonly GUIStyle lockToggleStyle;
		public static readonly GUIStyle activeToggleStyle;
		public static readonly GUIStyle miniLabelStyle;
		public static readonly GUIStyle tagStyle;
		public static readonly GUIStyle layerStyle;

		public static readonly GUIStyle labelNormal;
		public static readonly GUIStyle labelDisabled;
		public static readonly GUIStyle labelPrefab;
		public static readonly GUIStyle labelPrefabDisabled;
		public static readonly GUIStyle labelPrefabBroken;
		public static readonly GUIStyle labelPrefabBrokenDisabled;

		public static readonly Texture2D treeLineTexture;
		public static readonly Texture2D treeMiddleTexture;
		public static readonly Texture2D treeEndTexture;

		public static readonly Texture2D infoIcon;
		public static readonly Texture2D warningIcon;
		public static readonly Texture2D errorIcon;
		#endregion
	}

	/// <summary>
	/// <para>Guardar y cargar preferencias de jerarquía</para>
	/// </summary>
	internal static class Prefs
	{
		#region Preferencias
		static Prefs()
		{
			enabledContent = new GUIContent("Activar (Ctrl+H)", "Habilitar o deshabilitar el complemento completo, se deshabilitara automaticamente si se produce algun error.\n\nEl acceso directo solo funcionara si la ventana de jerarquia esta con el foco");
			offsetContent = new GUIContent("Offset", "Offset para los iconos, util si tienes mas extensiones que tambien utilizan la jerarquia");
			treeContent = new GUIContent("Hierarchy arbol", "Muestra las lineas que conectan las transformaciones a sus padres, utiles si tienes varios hijos dentro de los hijos");
			warningsContent = new GUIContent("Warnings", "Muestra iconos junto al gameobject con algun mensaje de error, advertencia o registro");
			tooltipsContent = new GUIContent("Tooltips", "Mostrar tooltips, esto mola ┌∩┐(◣_◢)┌∩┐");
			selectionContent = new GUIContent("Enhanced Seleccion", "Permitir seleccionar GameObjects arrastrandolos con el boton derecho del raton");
			stripsContent = new GUIContent("Separadores");
			miniLabelContent = new GUIContent("Mini label", "La label que aparece en el lado izquierdo de los iconos");
			staticAskContent = new GUIContent("Static cambio", "Como deben reaccionar los hijos de objetos al cambiar isStatic");
			trailingContent = new GUIContent("Taponado", "Añadir ... cuando los nombres son mas grandes que el area de vista");
			selectLockedContent = new GUIContent("Permitir seleccion bloqueada", "Permitir la seleccion de objetos que estan bloqueados");

			ReloadPrefs();
		}
		#endregion

		#region Variables
		public static int offset { get; private set; }
		public static bool tree { get; private set; }
		public static bool warning { get; private set; }
		public static bool tooltips { get; private set; }
		public static bool selection { get; private set; }
		public static bool trailing { get; private set; }
		public static bool selectLocked { get; private set; }
		public static TipoMiniLabel labelType { get; private set; }
		public static SeparadoresTipo separators { get; private set; }
		public static StaticMode staticAskMode { get; private set; }

		public static bool lineSeparator { get { return separators == (separators | SeparadoresTipo.Linea); } }
		public static bool colorSeparator { get { return separators == (separators | SeparadoresTipo.Color); } }
		public static bool objectOnlySeparator { get { return separators == (separators | SeparadoresTipo.Objetos); } }

		public static Color PlaymodeTint
		{
			get
			{
				if (!EditorApplication.isPlayingOrWillChangePlaymode)
					return Color.white;
				return (Color)playModeColorProp.GetValue(playModeColor, null);
			}
		}

		private static object playModeColor = typeof(Editor).Assembly.GetType("UnityEditor.HostView").GetField("kPlayModeDarken", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		private static PropertyInfo playModeColorProp = typeof(Editor).Assembly.GetType("UnityEditor.PrefColor").GetProperty("Color", BindingFlags.Instance | BindingFlags.Public);

		public static bool enabled
		{
			get { return EditorPrefs.GetBool("HierarchyEnabled", true); }
			set { EditorPrefs.SetBool("HierarchyEnabled", value); }
		}

		public static List<DrawTipo> drawOrder
		{
			get { return _drawOrder; }
			private set
			{
				EditorPrefs.SetInt("HierarchyDrawOrder" + "Count", value.Count);

				for (int i = 0; i < value.Count; i++)
				{
					EditorPrefs.SetInt("HierarchyDrawOrder" + i, (int)value[i]);
				}

				_drawOrder = value;
			}
		}

		private static readonly GUIContent enabledContent;
		private static readonly GUIContent offsetContent;
		private static readonly GUIContent treeContent;
		private static readonly GUIContent warningsContent;
		private static readonly GUIContent tooltipsContent;
		private static readonly GUIContent selectionContent;
		private static readonly GUIContent stripsContent;
		private static readonly GUIContent miniLabelContent;
		private static readonly GUIContent staticAskContent;
		private static readonly GUIContent trailingContent;
		private static readonly GUIContent selectLockedContent;

		private static Vector2 scroll;
		private static ReorderableList rList;
		private static List<DrawTipo> _drawOrder;
		#endregion

		#region Metodos
		private static GenericMenu menu
		{
			get
			{
				var menu = new GenericMenu();

				if (!rList.list.Contains(DrawTipo.Activo))
					menu.AddItem(new GUIContent("Activo"), false, () => rList.list.Add(DrawTipo.Activo));
				if (!rList.list.Contains(DrawTipo.Statico))
					menu.AddItem(new GUIContent("Statico"), false, () => rList.list.Add(DrawTipo.Statico));
				if (!rList.list.Contains(DrawTipo.Lock))
					menu.AddItem(new GUIContent("Bloqueado"), false, () => rList.list.Add(DrawTipo.Lock));
				if (!rList.list.Contains(DrawTipo.Icon))
					menu.AddItem(new GUIContent("Icono"), false, () => rList.list.Add(DrawTipo.Icon));
				if (!rList.list.Contains(DrawTipo.ApplyPrefab))
					menu.AddItem(new GUIContent("Aplicar Prefab"), false, () => rList.list.Add(DrawTipo.ApplyPrefab));
				if (!rList.list.Contains(DrawTipo.Tag))
					menu.AddItem(new GUIContent("Tag"), false, () => rList.list.Add(DrawTipo.Tag));
				if (!rList.list.Contains(DrawTipo.Layer))
					menu.AddItem(new GUIContent("Layer"), false, () => rList.list.Add(DrawTipo.Layer));

				return menu;
			}
		}

		/// <summary>
		/// <para>Carga las preferencias</para>
		/// </summary>
		private static void ReloadPrefs()// Carga las preferencias
		{
			offset = EditorPrefs.GetInt("HierarchyOffset", 2);
			tree = EditorPrefs.GetBool("HierarchyTree", true);
			warning = EditorPrefs.GetBool("HierarchyWarning", true);
			tooltips = EditorPrefs.GetBool("HierarchyTooltip", true);
			selection = EditorPrefs.GetBool("HierarchySelection", true);
			labelType = (TipoMiniLabel)EditorPrefs.GetInt("HierarchyMiniLabel", 3);
			separators = (SeparadoresTipo)EditorPrefs.GetInt("HierarchyStrips", -1);
			staticAskMode = (StaticMode)EditorPrefs.GetInt("HierarchyStaticMode", 2);
			trailing = EditorPrefs.GetBool("HierarchyTrailing", true);
			selectLocked = EditorPrefs.GetBool("HierarchySelectLocked", false);

			var list = new List<DrawTipo>();

			if (!EditorPrefs.HasKey("HierarchyDrawOrderCount"))
			{
				list.Add(DrawTipo.Icon);
				list.Add(DrawTipo.Activo);
				list.Add(DrawTipo.Lock);
				list.Add(DrawTipo.Statico);
				list.Add(DrawTipo.ApplyPrefab);
				list.Add(DrawTipo.Tag);
				list.Add(DrawTipo.Layer);
			}
			else
				for (int i = 0; i < EditorPrefs.GetInt("HierarchyDrawOrderCount"); i++)
					list.Add((DrawTipo)EditorPrefs.GetInt("HierarchyDrawOrder" + i));

			_drawOrder = list;

			rList = new ReorderableList(drawOrder, typeof(DrawTipo), true, true, true, true);
			rList.drawHeaderCallback += (rect) => { EditorGUI.LabelField(rect, "Botones", EditorStyles.boldLabel); };
			rList.onAddDropdownCallback += (rect, newList) => { menu.DropDown(rect); };
		}
		#endregion

		#region Menu
		[PreferenceItem("MHierarchy")]
		private static void OnPreferencesGUI()
		{
			try
			{
				scroll = EditorGUILayout.BeginScrollView(scroll, false, false);

				EditorGUILayout.Separator();
				GUI.enabled = enabled = EditorGUILayout.Toggle(enabledContent, enabled);
				EditorGUILayout.Separator();
				offset = EditorGUILayout.IntField(offsetContent, offset);
				EditorGUILayout.Separator();

				tree = EditorGUILayout.Toggle(treeContent, tree);
				warning = EditorGUILayout.Toggle(warningsContent, warning);
				tooltips = EditorGUILayout.Toggle(tooltipsContent, tooltips);
				selection = EditorGUILayout.Toggle(selectionContent, selection);
				trailing = EditorGUILayout.Toggle(trailingContent, trailing);
				selectLocked = EditorGUILayout.Toggle(selectLockedContent, selectLocked);

				EditorGUILayout.Separator();

				separators = (SeparadoresTipo)EditorGUILayout.EnumMaskField(stripsContent, separators);
				labelType = (TipoMiniLabel)EditorGUILayout.EnumPopup(miniLabelContent, labelType);
				GUI.enabled = enabled && drawOrder.Contains(DrawTipo.Statico);
				staticAskMode = (StaticMode)EditorGUILayout.EnumPopup(staticAskContent, staticAskMode);
				GUI.enabled = enabled;

				EditorGUILayout.Separator();

				drawOrder = rList.list.Cast<DrawTipo>().ToList();
				rList.displayAdd = menu.GetItemCount() > 0;
				rList.DoLayoutList();

				GUI.enabled = true;
				EditorGUILayout.EndScrollView();

				if (GUILayout.Button("Use Defaults", GUILayout.Width(120f)))
				{
					EditorPrefs.DeleteKey("HierarchyOffset");
					EditorPrefs.DeleteKey("HierarchyTree");
					EditorPrefs.DeleteKey("HierarchyWarning");
					EditorPrefs.DeleteKey("HierarchyTooltip");
					EditorPrefs.DeleteKey("HierarchySelection");
					EditorPrefs.DeleteKey("HierarchyStrips");
					EditorPrefs.DeleteKey("HierarchyMiniLabel");
					EditorPrefs.DeleteKey("HierarchyDrawOrderCount");
					EditorPrefs.DeleteKey("HierarchyStaticMode");
					EditorPrefs.DeleteKey("HierarchyTrailing");
					EditorPrefs.DeleteKey("HierarchySelectLocked");

					ReloadPrefs();
				}

				EditorPrefs.SetInt("HierarchyOffset", offset);
				EditorPrefs.SetBool("HierarchyTree", tree);
				EditorPrefs.SetBool("HierarchyWarning", warning);
				EditorPrefs.SetBool("HierarchyTooltip", tooltips);
				EditorPrefs.SetBool("HierarchySelection", selection);
				EditorPrefs.SetBool("HierarchyTrailing", trailing);
				EditorPrefs.SetBool("HierarchySelectLocked", selectLocked);
				EditorPrefs.SetInt("HierarchyStrips", (int)separators);
				EditorPrefs.SetInt("HierarchyMiniLabel", (int)labelType);
				EditorPrefs.SetInt("HierarchyStaticMode", (int)staticAskMode);

				Styles.ReloadTooltips();
				EditorApplication.RepaintHierarchyWindow();
			}
			catch (Exception e)
			{
				EditorGUILayout.HelpBox(e.ToString(), MessageType.Error);
			}
		}
		#endregion
	}

	/// <summary>
	/// <para>Misc Utilidades</para>
	/// </summary>
	internal static class Utility
	{
		#region Variables
		private static double lastTime;
		#endregion

		#region API
		/// <summary>
		/// <para>Muestra los FPS</para>
		/// </summary>
		/// <param name="evt"></param>
		/// <param name="rect"></param>
		public static void ShowFPS(Event evt, Rect rect)// Muestra los FPS
		{
			if (evt.type != EventType.Repaint)
				return;

			EditorGUI.DrawRect(rect, Color.yellow);
			var dTime = EditorApplication.timeSinceStartup - lastTime;
			EditorGUI.LabelField(rect, (1f / dTime).ToString("00.0") + " FPS");
			lastTime = EditorApplication.timeSinceStartup;
		}

		/// <summary>
		/// <para>Muestra el icono seleccionado</para>
		/// </summary>
		/// <param name="targetObj"></param>
		/// <param name="activatorRect"></param>
		/// <param name="showLabelIcons"></param>
		public static void ShowIconSelector(Object targetObj, Rect activatorRect, bool showLabelIcons)// Muestra el icono seleccionado
		{
			var type = typeof(Editor).Assembly.GetType("UnityEditor.IconSelector");
			var instance = ScriptableObject.CreateInstance(type);
			var parameters = new object[3];

			parameters[0] = targetObj;
			parameters[1] = activatorRect;
			parameters[2] = showLabelIcons;

			type.InvokeMember("Init", BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, instance, parameters);
		}

		/// <summary>
		/// <para>Encuenta o carga una informacion</para>
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static Texture2D FindOrLoad(byte[] bytes, string name)// Encuenta o carga una informacion
		{
			return FindTextureFromName(name) ?? ConvertToTexture(bytes, name);
		}

		/// <summary>
		/// <para>Convierte una data a textura</para>
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static Texture2D ConvertToTexture(byte[] bytes, string name)
		{
			var texture = new Texture2D(0, 0, TextureFormat.ARGB32, false, true);

			texture.LoadImage(bytes);
			texture.name = name;
			texture.alphaIsTransparency = true;
			texture.filterMode = FilterMode.Bilinear;
			texture.hideFlags = HideFlags.HideAndDontSave;
			texture.Apply();

			return texture;
		}

		/// <summary>
		/// <para>Buscar una textura por nombre</para>
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static Texture2D FindTextureFromName(string name)// Buscar una textura por nombre
		{
			var result = (from obj in Resources.FindObjectsOfTypeAll<Texture2D>()
						  where obj.name == name
						  select obj);

			return result.FirstOrDefault();
		}

		/// <summary>
		/// <para>Obtener el color</para>
		/// </summary>
		/// <param name="go"></param>
		/// <returns></returns>
		public static Color GetHierarchyColor(this GameObject go)// Obtener el color
		{
			if (!go)
				return Color.black;

			var prefabType = PrefabUtility.GetPrefabType(PrefabUtility.FindPrefabRoot(go));
			var active = go.activeInHierarchy;
			var style = active ? Styles.labelNormal : Styles.labelDisabled;

			switch (prefabType)
			{
				case PrefabType.PrefabInstance:
				case PrefabType.ModelPrefabInstance:
					style = active ? Styles.labelPrefab : Styles.labelPrefabDisabled;
					break;
				case PrefabType.MissingPrefabInstance:
					style = active ? Styles.labelPrefabBroken : Styles.labelPrefabBrokenDisabled;
					break;
			}

			return style.normal.textColor;
		}

		/// <summary>
		/// <para>Obtener el color</para>
		/// </summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static Color GetHierarchyColor(this Transform t)// Obtener el color
		{
			if (!t)
				return Color.black;

			return t.gameObject.GetHierarchyColor();
		}

		public static bool LastInHierarchy(this Transform t)
		{
			if (!t)
				return true;

			return t.parent.GetChild(t.parent.childCount - 1) == t;
		}

		public static bool LastInHierarchy(this GameObject go)
		{
			if (!go)
				return true;

			return go.transform.LastInHierarchy();
		}

		public static GUIStyle CreateStyleFromTextures(Texture2D on, Texture2D off)
		{
			var style = new GUIStyle();

			style.active.background = off;
			style.focused.background = off;
			style.hover.background = off;
			style.normal.background = off;
			style.onActive.background = on;
			style.onFocused.background = on;
			style.onHover.background = on;
			style.onNormal.background = on;
			style.imagePosition = ImagePosition.ImageOnly;
			style.fixedHeight = 15f;
			style.fixedWidth = 15f;

			return style;
		}
		#endregion
	}

	/// <summary>
	/// <para>LogEntry desde la consola, para comprobar si un objeto de juego tiene algun error o advertencia</para>
	/// </summary>
	internal struct LogEntry
	{
		#region Variables
		public int instanceID { get; private set; }
		public EntradaModo mode { get; private set; }
		public Object obj { get; private set; }
		public static Dictionary<Object, EntradaModo> referencedObjects { get; private set; }

		private const BindingFlags binding = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;

		private static readonly Type logEntriesType;
		private static readonly Type logEntryType;
		private static readonly MethodInfo getEntryMethod;
		private static readonly MethodInfo startMethod;
		private static readonly MethodInfo endMethod;
		private static readonly ConstructorInfo logEntryConstructor;

		private static readonly FieldInfo modeField;
		private static readonly FieldInfo instanceIDField;

		private static bool needLogReload;
		#endregion

		#region LOG
		static LogEntry()
		{
			try
			{
				logEntriesType = typeof(Editor).Assembly.GetType("UnityEditorInternal.LogEntries");
				logEntryType = typeof(Editor).Assembly.GetType("UnityEditorInternal.LogEntry");
				getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", binding);
				startMethod = logEntriesType.GetMethod("StartGettingEntries", binding);
				endMethod = logEntriesType.GetMethod("EndGettingEntries", binding);
				logEntryConstructor = logEntryType.GetConstructor(new Type[0]);

				modeField = logEntryType.GetField("mode");
				instanceIDField = logEntryType.GetField("instanceID");

				ReloadReferences();
			}
			catch (Exception e)
			{
				if (Prefs.warning)
					Debug.LogWarningFormat("MHierarchy, Error al obtener las entradas de la consola, si el error persiste, considere desactivar \"warning\" en las preferencias: {0}", e);
			}

			Application.logMessageReceivedThreaded += (logString, stackTrace, type) => {
				needLogReload = true;
			};

			EditorApplication.update += () => {
				if (needLogReload && Prefs.warning && Prefs.enabled)
				{
					ReloadReferences();
					needLogReload = false;
				}
			};
		}
		#endregion

		#region Metodos
		/// <summary>
		/// <para>Carga las preferencias</para>
		/// </summary>
		private static void ReloadReferences()// Carga las preferencias
		{
			try
			{
				referencedObjects = new Dictionary<Object, EntradaModo>();

				var logEntry = logEntryConstructor.Invoke(null);
				var entry = new LogEntry();
				var count = (int)startMethod.Invoke(null, null);

				for (int i = 0; i < count; i++)
				{
					getEntryMethod.Invoke(null, new object[] { i, logEntry });

					entry.mode = (EntradaModo)modeField.GetValue(logEntry);
					entry.instanceID = (int)instanceIDField.GetValue(logEntry);

					if (entry.instanceID != 0)
					{
						entry.obj = EditorUtility.InstanceIDToObject(entry.instanceID);

						if (entry.obj is GameObject)
						{
							if (referencedObjects.ContainsKey((entry.obj as GameObject).transform))
								referencedObjects[(entry.obj as GameObject).transform] |= entry.mode;
							else
								referencedObjects.Add((entry.obj as GameObject).transform, entry.mode);
						}
						else if (entry.obj is Component)
						{
							if (referencedObjects.ContainsKey((entry.obj as Component).transform))
								referencedObjects[(entry.obj as Component).transform] |= entry.mode;
							else
								referencedObjects.Add((entry.obj as Component).transform, entry.mode);
						}
					}
				}

				endMethod.Invoke(null, null);
			}
			catch { }
		}
		#endregion
	}
}
