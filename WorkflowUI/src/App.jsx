import React, { useState, useEffect, useCallback, useRef } from 'react';
import {
  ReactFlow,
  MiniMap,
  Controls,
  Background,
  useNodesState,
  useEdgesState,
  addEdge,
  Panel,
  Handle,
  Position,
  ReactFlowProvider,
  useReactFlow
} from '@xyflow/react';
import { 
  Play, 
  Square, 
  UserCheck, 
  GitBranch, 
  Webhook, 
  Plus, 
  Save, 
  ChevronLeft, 
  Trash2,
  Clock,
  AlertTriangle,
  FolderOpen
} from 'lucide-react';

// Custom Nodes Components
const StartEndNode = ({ data }) => {
  const isStart = data.label === 'شروع گردش';
  return (
    <div className={`px-4 py-3 bg-white border-2 ${isStart ? 'border-green-500' : 'border-red-500'} shadow-md rounded-xl min-w-[180px] text-right`} dir="rtl">
      {isStart && <Handle type="source" position={Position.Left} style={{ background: '#22c55e' }} />}
      <div className="flex items-center justify-center gap-1.5 font-black text-xs text-slate-800">
        {isStart ? <Play size={14} className="text-green-500" /> : <Square size={14} className="text-red-500" />}
        <span>{data.label}</span>
      </div>
      {!isStart && <Handle type="target" position={Position.Right} style={{ background: '#ef4444' }} />}
    </div>
  );
};

const ApprovalNode = ({ data, selected }) => {
  return (
    <div className={`px-4 py-3 bg-white border-2 ${selected ? 'border-teal-500 ring-2 ring-teal-500/20' : 'border-gray-200'} shadow-md rounded-xl min-w-[180px] text-right`} dir="rtl">
      <Handle type="target" position={Position.Right} />
      <div className="flex items-center gap-1.5 font-bold text-xs border-b border-gray-100 pb-1.5 text-slate-800">
        <UserCheck size={14} className="text-teal-600" />
        <span>{data.label || 'بررسی و تایید'}</span>
      </div>
      <div className="text-[10px] text-slate-500 space-y-1 mt-1.5">
        <div>نوع انتساب: {
          data.assignmentMode === 'User' ? 'کاربر مشخص' :
          data.assignmentMode === 'Role' ? 'نقش کاربری' :
          data.assignmentMode === 'Department' ? 'دپارتمان' : 'نامشخص'
        }</div>
        {data.slaHours && <div>SLA: {data.slaHours} ساعت</div>}
      </div>
      <Handle type="source" position={Position.Left} />
    </div>
  );
};

const ConditionNode = ({ data, selected }) => {
  return (
    <div className={`px-4 py-3 bg-white border-2 ${selected ? 'border-amber-500 ring-2 ring-amber-500/20' : 'border-amber-200'} shadow-md rounded-xl min-w-[180px] text-right`} dir="rtl">
      <Handle type="target" position={Position.Right} />
      <div className="flex items-center gap-1.5 font-bold text-xs border-b border-gray-100 pb-1.5 text-slate-800">
        <GitBranch size={14} className="text-amber-600" />
        <span>{data.label || 'شرط منطقی'}</span>
      </div>
      <div className="text-[10px] text-slate-500 mt-1.5">
        {data.condition ? `شرط: ${data.condition}` : 'بدون شرط'}
      </div>
      <Handle type="source" position={Position.Left} />
    </div>
  );
};

const WebhookNode = ({ data, selected }) => {
  return (
    <div className={`px-4 py-3 bg-white border-2 ${selected ? 'border-indigo-500 ring-2 ring-indigo-500/20' : 'border-indigo-200'} shadow-md rounded-xl min-w-[180px] text-right`} dir="rtl">
      <Handle type="target" position={Position.Right} />
      <div className="flex items-center gap-1.5 font-bold text-xs border-b border-gray-100 pb-1.5 text-slate-800">
        <Webhook size={14} className="text-indigo-600" />
        <span>{data.label || 'وب‌هوک'}</span>
      </div>
      <div className="text-[10px] text-slate-500 mt-1.5">
        سرویس خارجی
      </div>
      <Handle type="source" position={Position.Left} />
    </div>
  );
};

const nodeTypes = {
  StartEndNode,
  ApprovalNode,
  ConditionNode,
  WebhookNode
};

function App() {
  const [documentType, setDocumentType] = useState('Letter');
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);
  
  const [selectedNode, setSelectedNode] = useState(null);
  const [selectedEdge, setSelectedEdge] = useState(null);
  
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [departments, setDepartments] = useState([]);

  useEffect(() => {
    if (window.WorkflowBuilderData) {
      setUsers(window.WorkflowBuilderData.users || []);
      setRoles(window.WorkflowBuilderData.roles || []);
      setDepartments(window.WorkflowBuilderData.departments || []);
      if (window.WorkflowBuilderData.documentType) {
        setDocumentType(window.WorkflowBuilderData.documentType);
      }
    }
  }, []);

  useEffect(() => {
    fetch(`/Security/GetWorkflowGraph?documentType=${documentType}`)
      .then(res => res.json())
      .then(data => {
        const rfNodes = data.nodes.map(n => ({
          id: n.id,
          type: n.type,
          position: { x: n.x, y: n.y },
          data: { 
            label: n.label,
            assignmentMode: n.assignmentMode || 'User',
            assignedToUserId: n.assignedToUserId || '',
            assignedRoleId: n.assignedRoleId || '',
            assignedDepartmentId: n.assignedDepartmentId || '',
            slaHours: n.slaHours || 24,
            escalationHours: n.escalationHours || 48
          }
        }));

        const rfEdges = data.edges.map(e => ({
          id: e.id,
          source: e.source,
          target: e.target,
          label: e.condition || ''
        }));

        setNodes(rfNodes);
        setEdges(rfEdges);
        setSelectedNode(null);
        setSelectedEdge(null);
      })
      .catch(err => console.error("Error loading workflow graph", err));
  }, [documentType, setNodes, setEdges]);

  const onConnect = useCallback(
    (params) => setEdges((eds) => addEdge(params, eds)),
    [setEdges]
  );

  const onNodeClick = useCallback((event, node) => {
    setSelectedNode(node);
    setSelectedEdge(null);
  }, []);

  const onEdgeClick = useCallback((event, edge) => {
    setSelectedEdge(edge);
    setSelectedNode(null);
  }, []);

  const reactFlowWrapper = useRef(null);
  const { screenToFlowPosition } = useReactFlow();

  const onDragOver = useCallback((event) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback((event) => {
    event.preventDefault();
    const type = event.dataTransfer.getData('application/reactflow');
    if (typeof type === 'undefined' || !type) return;
    const position = screenToFlowPosition({ x: event.clientX, y: event.clientY });
    const id = `new-step-${Date.now()}`;
    const newNode = {
      id,
      type,
      position,
      data: { 
        label: type === 'ApprovalNode' ? 'تایید جدید' : type === 'ConditionNode' ? 'شرط جدید' : 'وب‌هوک جدید',
        assignmentMode: 'User',
        assignedToUserId: '',
        assignedRoleId: '',
        assignedDepartmentId: '',
        slaHours: 24,
        escalationHours: 48
      }
    };
    setNodes((nds) => nds.concat(newNode));
  }, [setNodes, screenToFlowPosition]);

  const updateNodeData = (nodeId, updatedFields) => {
    setNodes((nds) =>
      nds.map((node) => {
        if (node.id === nodeId) {
          const newData = { ...node.data, ...updatedFields };
          setSelectedNode(prev => prev && prev.id === nodeId ? { ...prev, data: newData } : prev);
          return { ...node, data: newData };
        }
        return node;
      })
    );
  };

  const saveWorkflow = () => {
    const orphaned = nodes.filter(n => {
      if (n.id === 'start' || n.id === 'end') return false;
      const hasConnection = edges.some(e => e.source === n.id || e.target === n.id);
      return !hasConnection;
    });

    if (orphaned.length > 0) {
      alert(`خطا: نودهای بدون اتصال پیدا شدند:\n${orphaned.map(n => n.data.label).join('، ')}`);
      return;
    }

    const hasStart = nodes.some(n => n.id === 'start');
    const hasEnd = nodes.some(n => n.id === 'end');
    if (!hasStart || !hasEnd) {
      alert('خطا: گراف باید حتماً دارای یک نقطه شروع و یک نقطه پایان متصل باشد.');
      return;
    }

    const payload = {
      documentType,
      nodes: nodes.map(n => ({
        id: n.id,
        type: n.type,
        label: n.data.label,
        x: n.position.x,
        y: n.position.y,
        assignmentMode: n.data.assignmentMode,
        assignedToUserId: n.data.assignedToUserId ? String(n.data.assignedToUserId) : null,
        assignedRoleId: n.data.assignedRoleId ? String(n.data.assignedRoleId) : null,
        assignedDepartmentId: n.data.assignedDepartmentId ? parseInt(n.data.assignedDepartmentId) : null,
        slaHours: parseInt(n.data.slaHours) || 24,
        escalationHours: parseInt(n.data.escalationHours) || 48
      })),
      edges: edges.map(e => ({
        id: e.id,
        source: e.source,
        target: e.target,
        condition: e.label || ''
      }))
    };

    fetch('/Security/SaveWorkflowGraph', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
      },
      body: JSON.stringify(payload)
    })
    .then(res => res.json())
    .then(data => {
      if (data.success) {
        alert(data.message);
        window.location.reload();
      } else {
        alert('بروز خطا: ' + data.message);
      }
    })
    .catch(err => alert('خطا در ذخیره‌سازی گردش کار: ' + err));
  };

  const deleteNode = (nodeId) => {
    if (nodeId === 'start' || nodeId === 'end') return;
    setNodes(nds => nds.filter(n => n.id !== nodeId));
    setEdges(eds => eds.filter(e => e.source !== nodeId && e.target !== nodeId));
    setSelectedNode(null);
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%' }} className="select-none text-right">
      
      {/* Sidebar Tool Box (Drag & Drop panel) */}
      <aside style={{ width: '100%', maxHeight: '30%', overflowY: 'auto', padding: '16px', backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', flexShrink: 0 }}>
        <div>
          <h3 className="text-xs font-black text-slate-800 dark:text-slate-200 flex items-center gap-1.5">
            <FolderOpen size={14} className="text-teal-600" />
            انتخاب نوع سند
          </h3>
          <select 
            value={documentType} 
            onChange={(e) => setDocumentType(e.target.value)}
            className="w-full text-xs mt-2 px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-200 outline-none"
          >
            <option value="Letter">نامه (Letter)</option>
            <option value="Financial">مالی (Financial)</option>
            <option value="Warehouse">انبار (Warehouse)</option>
          </select>
        </div>

        <div style={{ marginTop: '12px' }}>
          <h3 className="text-xs font-black text-slate-800 dark:text-slate-200">کیت ابزار (Drag & Drop)</h3>
          <p className="text-[10px] text-slate-400 mt-1">گزینه‌های زیر را بکشید و روی بوم رها کنید:</p>
          
          <div className="space-y-2 mt-3">
            <div 
              draggable 
              onDragStart={(e) => { e.dataTransfer.setData('application/reactflow', 'ApprovalNode'); e.dataTransfer.effectAllowed = 'move'; }}
              className="flex items-center gap-2 p-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg cursor-grab hover:border-teal-500 transition text-xs font-bold"
            >
              <UserCheck size={14} className="text-teal-600" />
              <span>مرحله بررسی و تایید</span>
            </div>
            
            <div 
              draggable 
              onDragStart={(e) => { e.dataTransfer.setData('application/reactflow', 'ConditionNode'); e.dataTransfer.effectAllowed = 'move'; }}
              className="flex items-center gap-2 p-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg cursor-grab hover:border-amber-500 transition text-xs font-bold"
            >
              <GitBranch size={14} className="text-amber-500" />
              <span>شرط منطقی (Gateway)</span>
            </div>

            <div 
              draggable 
              onDragStart={(e) => { e.dataTransfer.setData('application/reactflow', 'WebhookNode'); e.dataTransfer.effectAllowed = 'move'; }}
              className="flex items-center gap-2 p-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg cursor-grab hover:border-indigo-500 transition text-xs font-bold"
            >
              <Webhook size={14} className="text-indigo-500" />
              <span>وب‌هوک اتصال به وب</span>
            </div>
          </div>
        </div>
      </aside>

      {/* Main area: Canvas + Properties Panels side by side */}
      <div style={{ flex: 1, width: '100%', minHeight: 0, display: 'flex', flexDirection: 'row' }}>
        
        {/* Canvas */}
        <div style={{ flex: 1, minWidth: 0, minHeight: '500px', position: 'relative' }} ref={reactFlowWrapper} onDragOver={onDragOver} onDrop={onDrop}>
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            nodeTypes={nodeTypes}
            onNodeClick={onNodeClick}
            onEdgeClick={onEdgeClick}
            fitView
            style={{ width: '100%', height: '100%' }}
          >
            <Panel position="top-left" className="p-2 backdrop-blur flex gap-2">
              <button 
                onClick={saveWorkflow}
                className="bg-teal-600 hover:bg-teal-700 text-white px-5 py-2.5 rounded-xl shadow-lg font-semibold flex items-center gap-2 transition-all"
              >
                <Save size={14} />
                <span>ذخیره گردش کار</span>
              </button>
            </Panel>
            <Controls />
            <MiniMap nodeStrokeWidth={3} />
            <Background variant="dots" gap={12} size={1} />
          </ReactFlow>
        </div>

        {/* Node Properties Panel - fixed width sidebar */}
        {selectedNode && (
          <aside style={{ width: '300px', minWidth: '300px', flexShrink: 0, overflowY: 'auto', backgroundColor: '#fff', borderLeft: '1px solid #e5e7eb', padding: '16px' }} className="dark:bg-slate-950 dark:border-slate-800">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-2">
              <h3 className="text-xs font-black text-slate-800 dark:text-slate-200 flex items-center gap-1">
                <Clock size={14} className="text-teal-600" />
                تنظیمات نود انتخابی
              </h3>
              <button onClick={() => setSelectedNode(null)} className="text-slate-400 hover:text-slate-600">
                <ChevronLeft size={16} />
              </button>
            </div>

            <div className="space-y-3 text-xs" style={{ marginTop: '12px' }}>
              <div className="flex flex-col gap-1">
                <label className="font-bold text-slate-600 dark:text-slate-400">عنوان مرحله</label>
                <input 
                  type="text" 
                  value={selectedNode.data.label}
                  onChange={(e) => updateNodeData(selectedNode.id, { label: e.target.value })}
                  className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                />
              </div>

              {selectedNode.type === 'ApprovalNode' && (
                <>
                  <div className="flex flex-col gap-1">
                    <label className="font-bold text-slate-600 dark:text-slate-400">نوع انتساب کار</label>
                    <select 
                      value={selectedNode.data.assignmentMode}
                      onChange={(e) => updateNodeData(selectedNode.id, { assignmentMode: e.target.value })}
                      className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                    >
                      <option value="User">کاربر مشخص (User)</option>
                      <option value="Role">نقش کاربری (Role)</option>
                      <option value="Department">دپارتمان (Department)</option>
                    </select>
                  </div>

                  {selectedNode.data.assignmentMode === 'User' && (
                    <div className="flex flex-col gap-1">
                      <label className="font-bold text-slate-600 dark:text-slate-400">کاربر مسئول تایید</label>
                      <select 
                        value={selectedNode.data.assignedToUserId}
                        onChange={(e) => updateNodeData(selectedNode.id, { assignedToUserId: e.target.value })}
                        className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                      >
                        <option value="">-- انتخاب کاربر --</option>
                        {users.map(u => <option key={u.id} value={u.id}>{u.fullName}</option>)}
                      </select>
                    </div>
                  )}

                  {selectedNode.data.assignmentMode === 'Role' && (
                    <div className="flex flex-col gap-1">
                      <label className="font-bold text-slate-600 dark:text-slate-400">نقش کاربری مسئول تایید</label>
                      <select 
                        value={selectedNode.data.assignedRoleId}
                        onChange={(e) => updateNodeData(selectedNode.id, { assignedRoleId: e.target.value })}
                        className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                      >
                        <option value="">-- انتخاب نقش --</option>
                        {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                      </select>
                    </div>
                  )}

                  {selectedNode.data.assignmentMode === 'Department' && (
                    <div className="flex flex-col gap-1">
                      <label className="font-bold text-slate-600 dark:text-slate-400">دپارتمان مسئول تایید</label>
                      <select 
                        value={selectedNode.data.assignedDepartmentId}
                        onChange={(e) => updateNodeData(selectedNode.id, { assignedDepartmentId: e.target.value })}
                        className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                      >
                        <option value="">-- انتخاب دپارتمان --</option>
                        {departments.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
                      </select>
                    </div>
                  )}

                  <div className="flex flex-col gap-1">
                    <label className="font-bold text-slate-600 dark:text-slate-400">حداکثر مهلت پاسخگویی (SLA - ساعت)</label>
                    <input 
                      type="number" 
                      value={selectedNode.data.slaHours}
                      onChange={(e) => updateNodeData(selectedNode.id, { slaHours: e.target.value })}
                      className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <label className="font-bold text-slate-600 dark:text-slate-400">مهلت ارجاع خودکار (ساعت)</label>
                    <input 
                      type="number" 
                      value={selectedNode.data.escalationHours}
                      onChange={(e) => updateNodeData(selectedNode.id, { escalationHours: e.target.value })}
                      className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                    />
                  </div>
                </>
              )}

              {selectedNode.type !== 'StartEndNode' && (
                <button 
                  onClick={() => deleteNode(selectedNode.id)}
                  className="w-full mt-4 py-2 border border-rose-200 hover:bg-rose-50 dark:border-rose-900/30 dark:hover:bg-rose-950/20 text-rose-600 rounded-lg transition flex items-center justify-center gap-1.5 font-bold"
                >
                  <Trash2 size={14} />
                  <span>حذف این نود</span>
                </button>
              )}
            </div>
          </aside>
        )}

        {/* Edge Properties Panel - fixed width sidebar */}
        {selectedEdge && (
          <aside style={{ width: '300px', minWidth: '300px', flexShrink: 0, overflowY: 'auto', backgroundColor: '#fff', borderLeft: '1px solid #e5e7eb', padding: '16px' }} className="dark:bg-slate-950 dark:border-slate-800">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-2">
              <h3 className="text-xs font-black text-slate-800 dark:text-slate-200 flex items-center gap-1">
                <AlertTriangle size={14} className="text-amber-500" />
                تنظیمات یال (رابط)
              </h3>
              <button onClick={() => setSelectedEdge(null)} className="text-slate-400 hover:text-slate-600">
                <ChevronLeft size={16} />
              </button>
            </div>

            <div className="space-y-3 text-xs" style={{ marginTop: '12px' }}>
              <div className="flex flex-col gap-1">
                <label className="font-bold text-slate-600 dark:text-slate-400">شرط انتقال (مانند: مبلغ {'>'} ۱۰۰ میلیون)</label>
                <input 
                  type="text" 
                  value={selectedEdge.label || ''}
                  onChange={(e) => {
                    const val = e.target.value;
                    setEdges(eds => eds.map(edge => edge.id === selectedEdge.id ? { ...edge, label: val } : edge));
                    setSelectedEdge(prev => prev ? { ...prev, label: val } : null);
                  }}
                  className="px-3 py-2 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-lg text-slate-800 dark:text-slate-100 outline-none"
                  placeholder="مثلاً: amount > 100000000"
                />
              </div>
              
              <button 
                onClick={() => {
                  setEdges(eds => eds.filter(e => e.id !== selectedEdge.id));
                  setSelectedEdge(null);
                }}
                className="w-full mt-4 py-2 border border-rose-200 hover:bg-rose-50 dark:border-rose-900/30 dark:hover:bg-rose-950/20 text-rose-600 rounded-lg transition flex items-center justify-center gap-1.5 font-bold"
              >
                <Trash2 size={14} />
                <span>حذف این یال</span>
              </button>
            </div>
          </aside>
        )}
      </div>
    </div>
  );
}

export default function AppWithProvider() {
  return (
    <ReactFlowProvider>
      <App />
    </ReactFlowProvider>
  );
}