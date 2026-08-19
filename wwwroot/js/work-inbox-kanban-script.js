let draggedTaskId = null;

        function handleDragStart(event, taskId) {
            draggedTaskId = taskId;
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', taskId);
            event.target.classList.add('opacity-50');
        }

        function handleDragOver(event) {
            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';
        }

        function handleDrop(event, status) {
            event.preventDefault();
            const taskId = event.dataTransfer.getData('text/plain') || draggedTaskId;
            if (!taskId) return;

            const cardEl = document.querySelector(`[data-task-id="${taskId}"]`);
            if (cardEl) {
                cardEl.classList.remove('opacity-50');
            }

            const formData = new FormData();
            formData.append('taskId', taskId);
            formData.append('status', status);

            fetch('/WorkInbox/UpdateTaskStatus', {
                method: 'POST',
                body: formData,
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                }
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    location.reload();
                } else {
                    alert('خطا در به‌روزرسانی وضعیت');
                }
            })
            .catch(() => alert('خطا در ارتباط با سرور'));

            draggedTaskId = null;
        }
