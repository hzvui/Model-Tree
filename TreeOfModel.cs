using System.Collections;
using System.Collections.Generic;
using Unigine;
using System.IO;

public class TreeOfModel
{
    public WidgetTreeBox treeBox;
    public WidgetScrollBox widgetScrollBox;
    public bool show;
    // private EngineWindowViewport modelTree;
    private WidgetVBox widgetVBoxTree;

    private int indexObjectCur = 0;
    private Object clickedObject = null;
    private Node clickedNode = null;

    Gui gui;
    private ObjectGui objectGui;

    // private Image icons = new Image();
    private string path = "";
    private Texture texture = null;
    private Dictionary<int, Material> initialMaterials = [];

    private Material materalTransparent = null;

    private GetterModels getterModels;

    public static Node myNode = null;
    private int lastClickedIndex = -1;
    private long lastClickTime = 0;
    private const long DOUBLE_CLICK_INTERVAL = 300; // миллисекунд

    public TreeOfModel(GetterModels getter, ObjectGui guii)
    {
        // Принимаем зависимости извне — доверяем тому, кто нас создаёт
        getterModels = getter;
        objectGui = guii;
        gui = objectGui.GetGui();

        path = "data\\pics\\treeBoxEyes.png";
        materalTransparent = Materials.LoadMaterial("../data/models/materials/transparent.mat");

        // НИКАКОГО поиска через World.GetNodeByName — он больше не нужен!
        // Мы уже получили всё необходимое через параметры

        objectGui.MouseMode = ObjectGui.MOUSE_VIRTUAL;

        widgetVBoxTree = new();
        widgetVBoxTree.Background = 1;
        widgetScrollBox = new();
        widgetScrollBox.Background = 1;
        widgetVBoxTree.AddChild(widgetScrollBox, Gui.ALIGN_LEFT | Gui.ALIGN_TOP | Gui.ALIGN_EXPAND);
        gui.AddChild(widgetVBoxTree, Gui.ALIGN_LEFT | Gui.ALIGN_TOP | Gui.ALIGN_EXPAND);

        show = true;
    }

    public void GetNode()
    {
        myNode = getterModels.GetNode();
    }

    public void CreateTree(int ModelId)
    {
        if (!World.GetNodeByID(ModelId)) return;

        var node = World.GetNodeByID(ModelId);

        indexObjectCur = 0;
        AssignNodeIndices(node);
        while (gui.NumChildren != 0)
        {
            gui.RemoveChild(gui.GetChild(0));
        }

        treeBox = new WidgetTreeBox();
        treeBox.FontSize = 30;
        treeBox.Texture = path;

        SetTreeToNode(node);

        widgetVBoxTree = new();
        widgetVBoxTree.Background = 1;
        widgetScrollBox = new();
        widgetScrollBox.Background = 1;
        widgetScrollBox.AddChild(treeBox);
        widgetVBoxTree.AddChild(widgetScrollBox, Gui.ALIGN_LEFT | Gui.ALIGN_TOP | Gui.ALIGN_EXPAND);
        gui.AddChild(widgetVBoxTree, Gui.ALIGN_LEFT | Gui.ALIGN_TOP | Gui.ALIGN_EXPAND);

        treeBox.MultiSelection = true;
        treeBox.EventClicked.Connect(() => TreeBoxClicked(show));
    }

    private void SetStateToObject(Node currentNode, int index, bool state)
    {
        var obj = currentNode as Object;
        Material mat = null;

        if (currentNode.GetData("TreeIndex") == index.ToString())
        {
            for (int i = 0; i < currentNode.NumChildren; i++)
            {
                if (currentNode.GetChild(i).NumChildren != 0)
                {
                    int indexChild = int.Parse(currentNode.GetChild(i).GetData("TreeIndex"));
                    SetStateToObject(currentNode.GetChild(i), indexChild, state);
                }
                else
                {
                    var childObj = currentNode.GetChild(i) as Object;
                    int childIndex = int.Parse(currentNode.GetChild(i).GetData("TreeIndex"));
                    if (childObj == null) continue;

                    if (state)
                        mat = initialMaterials[childIndex];
                    else
                        mat = materalTransparent;

                    for (int j = 0; j < childObj.NumSurfaces; j++)
                    {
                        childObj.SetMaterial(mat, j);
                        childObj.SetIntersection(state, j);
                    }
                }
            }

            if (obj == null) return;

            if (state)
                mat = initialMaterials[index];
            else 
                mat = materalTransparent;

            for (int j = 0; j < obj.NumSurfaces; j++)
            {
                obj.SetMaterial(mat, j);
                obj.SetIntersection(state, j);
            }
        }
        else
        {
            for (int i = 0; i < currentNode.NumChildren; i++)
            {
                SetStateToObject(currentNode.GetChild(i), index, state);
            }
        }
    }

    private void GetObjectFromIndex(Node currentNode, int index)
    {
        if (currentNode.GetData("TreeIndex") == index.ToString())
        {
            clickedObject = currentNode as Object;
        }

        for (int i = 0; i < currentNode.NumChildren; i++)
        {
            GetObjectFromIndex(currentNode.GetChild(i), index);
        }
    }

    private void GetNodeFromIndex(Node currentNode, int index)
    {
        if (currentNode.GetData("TreeIndex") == index.ToString())
        {
            clickedNode = currentNode;
        }

        for (int i = 0; i < currentNode.NumChildren; i++)
        {
            GetNodeFromIndex(currentNode.GetChild(i), index);
        }
    }

    private void SelectAllClickedNodes(Node selectNode)
    {
        var obj = selectNode as Object;
        if (obj != null)
        {
            Visualizer.RenderObjectSurfaceBoundBox(obj, 0, vec4.BLUE, 2.0f);
            Visualizer.RenderObject(obj, vec4.BLUE, 2.0f);
        }
        for (int i = 0; i < selectNode.NumChildren; i++)
        {
            var currentNode = selectNode.GetChild(i);
            SelectAllClickedNodes(currentNode);
        }
    }
    
    private void TreeBoxClicked(bool show)
    {
        int index = treeBox.GetSelectedItem(0);
        if (index == -1) return;

        long currentTime = System.DateTime.Now.Ticks / 10000; // в миллисекундах

        if (show)
        {
            // Проверяем, не двойной ли клик
            if (index == lastClickedIndex && (currentTime - lastClickTime) <= DOUBLE_CLICK_INTERVAL)
            {
                // Это двойной клик!
                TreeBoxDoubleClicked();
                lastClickedIndex = -1;
                lastClickTime = 0;
                return;
            }

            // Обычный одиночный клик
            GetNodeFromIndex(World.GetNodeByID(getterModels.GetNodeID()), index);
            if (clickedNode == null) return;
            SelectAllClickedNodes(clickedNode);
        }

        // Запоминаем для возможного дабл-клика
        lastClickedIndex = index;
        lastClickTime = currentTime;
    }

    private void TreeBoxDoubleClicked()
    {
        int index = treeBox.GetSelectedItem(0);
        if (index == -1) return;

        Node rootNode = World.GetNodeByID(getterModels.GetNodeID());
        if (rootNode == null) return;

        vec4 redClr = new vec4(1, 0.2f, 0, 1);
        bool isCurrentlyRed = treeBox.GetItemColor(index) == redClr;
        bool newState = isCurrentlyRed; // true = показать, false = скрыть

        vec4 newColor = isCurrentlyRed ? vec4.WHITE : redClr;
        treeBox.SetItemColor(index, newColor);
        SetStateToObject(rootNode, index, newState);
        SetColorToTreeBox(index, newColor);
    }

    private void SetColorToTreeBox(int index, vec4 color)
    {
        treeBox.SetItemColor(index, color);

        for (int i = 0; i < treeBox.GetNumItemChildren(index); i++)
        {
            int childIndex = treeBox.GetItemChild(index, i);
            SetColorToTreeBox(childIndex, color);
        }
    }

    public void ChangeCurrentMaterialState()
    {
        int index = treeBox.GetSelectedItem(0);
        if (index == -1) return;

        vec4 redClr = new vec4(1, 0.2, 0, 1);

        bool isCurrentlyRed = treeBox.GetItemColor(index) == redClr;
        vec4 newColor = isCurrentlyRed ? vec4.WHITE : redClr;
        bool newState = isCurrentlyRed;

        treeBox.SetItemColor(index, newColor);
        SetStateToObject(myNode, index, newState);
        SetColorToTreeBox(index, newColor);
    }

    private void SetStateToAllObject(Node currentNode, int index, bool state)
    {
        var obj = currentNode as Object;
        Material mat = null;
        int stat = state ? 1 : 0;

        if (currentNode.GetData("TreeIndex") == index.ToString())
        {
            for (int i = 0; i < currentNode.NumChildren; i++)
            {
                if (currentNode.GetChild(i).NumChildren != 0)
                {
                    int indexChild = int.Parse(currentNode.GetChild(i).GetData("TreeIndex"));
                    SetStateToAllObject(currentNode.GetChild(i), indexChild, state);
                }
                else
                {
                    var childObj = currentNode.GetChild(i) as Object;
                    int childIndex = int.Parse(currentNode.GetChild(i).GetData("TreeIndex"));
                    if (childObj == null) continue;

                    if (state)
                        mat = initialMaterials[childIndex];
                    else
                        mat = materalTransparent;

                    for (int j = 0; j < childObj.NumSurfaces; j++)
                    {
                        childObj.SetMaterial(mat, j);
                        childObj.SetIntersection(state, j);
                    }
                }
            }

            if (obj == null) return;

            if (state)
                mat = initialMaterials[index];
            else
                mat = materalTransparent;

            for (int j = 0; j < obj.NumSurfaces; j++)
            {
                obj.SetMaterial(mat, j);
                obj.SetIntersection(state, j);
            }
        }
        else
        {
            for (int i = 0; i < currentNode.NumChildren; i++)
            {
                SetStateToAllObject(currentNode.GetChild(i), index, state);
            }
        }
    }

    public void ChangeAllMaterialsState()
    {
        int index = treeBox.GetSelectedItem(0);
        if (index == -1) return;

        vec4 redClr = new vec4(0.2, 1, 0, 1);

        bool isCurrentlyRed = treeBox.GetItemColor(index) == redClr;
        vec4 newColor = isCurrentlyRed ? vec4.WHITE : redClr;
        bool newState = isCurrentlyRed;

        treeBox.SetItemColor(index, newColor);
        SetColorToTreeBox(0, vec4.WHITE);
        if (!newState)
        {
            SetStateToObject(myNode, 0, false);
            SetStateToObject(myNode, index, true);
        }
        else
        {
            SetStateToObject(myNode, 0, true);
        }

        SetColorToTreeBox(index, newColor);
    }

    private void AssignNodeIndices(Node currentNode)
    {
        string strIndex = indexObjectCur.ToString();
        currentNode.SetData("TreeIndex", strIndex);
        var obj = currentNode as Object;
        if (obj != null)
        {
            Material mat = obj.GetMaterial(0);
            currentNode.SetData("CustMat", mat.FilePath);

            initialMaterials.Add(indexObjectCur, mat);
        }

        Log.Message($"Узел: {currentNode.Name}, индекс: {indexObjectCur}\n");

        indexObjectCur++;

        for (int i = 0; i < currentNode.NumChildren; i++)
        {
            AssignNodeIndices(currentNode.GetChild(i));
        }
    }

    private void SetTreeToNode(Node crossNode, int parentIndex = -1)
    {
        int currentIndex = treeBox.AddItem(" " + crossNode.Name, 1);
        if (parentIndex != -1)
        {
            treeBox.AddItemChild(parentIndex, currentIndex);
        }

        for (var i = 0; i < crossNode.NumChildren; i++)
        {
            SetTreeToNode(crossNode.GetChild(i), currentIndex);
        }
    }

    public void ShowSelectedItem(int index)
    {
        treeBox.ShowItem(index);

        for (int i = 0; i < treeBox.NumItems; i++)
        {
            treeBox.SetItemSelected(i, 0);
        }
        treeBox.SetItemSelected(index, 1);
    }
}