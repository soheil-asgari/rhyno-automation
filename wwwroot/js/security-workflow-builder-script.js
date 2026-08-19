(() => {
    const el = document.getElementById('workflowBuilderData');
    const data = el ? JSON.parse(el.textContent) : {};
    window.WorkflowBuilderData = {
        documentType: data.documentType ?? 'Letter',
        users: data.users ?? [],
        roles: data.roles ?? [],
        departments: data.departments ?? []
    };
})();
