import bpy, json, os
result = {'file': bpy.data.filepath, 'materials': [], 'groups': [], 'objects': []}
def tree_data(tree):
    return {'name': tree.name, 'nodes': [{
        'name': n.name, 'type': n.bl_idname,
        'image': getattr(getattr(n,'image',None),'filepath',None),
        'group': getattr(getattr(n,'node_tree',None),'name',None),
        'operation': getattr(n,'operation',None),
        'inputs': {i.name: str(i.default_value) for i in n.inputs if hasattr(i,'default_value')},
    } for n in tree.nodes], 'links': [
        [l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name] for l in tree.links]}
for m in bpy.data.materials:
    if m.use_nodes: result['materials'].append(tree_data(m.node_tree))
for g in bpy.data.node_groups: result['groups'].append(tree_data(g))
for o in bpy.data.objects:
    result['objects'].append({'name': o.name, 'type': o.type,
        'materials': [s.material.name if s.material else None for s in o.material_slots],
        'modifiers': [{'name':m.name,'type':m.type,'group':getattr(getattr(m,'node_group',None),'name',None)} for m in o.modifiers]})
path=os.path.join(r'C:\__dev\quentin\SML-First-Game\Tools\ReiLeafShader',os.path.basename(bpy.data.filepath)+'.json')
with open(path,'w') as f: json.dump(result,f,indent=2)
print('INSPECT_REPORT',path)
