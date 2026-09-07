(() => {
    const roleLabels = Object.freeze({
        Admin: 'Yönetici',
        Laboratory: 'Laboratuvar çalışanı',
        Field: 'Saha çalışanı',
        Manager: 'Birim yöneticisi'
    });

    const registerView = document.getElementById('register-view');
    const registerForm = document.getElementById('register-form');
    const registerMessage = document.getElementById('register-message');
    const createUserView = document.getElementById('create-user-view');
    const createUserForm = document.getElementById('create-user-form');
    const createUserMessage = document.getElementById('create-user-message');
    const userDetailView = document.getElementById('user-detail-view');
    const userEditForm = document.getElementById('user-edit-form');
    const userEditMessage = document.getElementById('user-edit-message');
    const userTable = document.getElementById('user-table');
    const userSearch = document.getElementById('user-search');
    const userRoleFilter = document.getElementById('user-role-filter');
    const userActiveFilter = document.getElementById('user-active-filter');
    const profileForm = document.getElementById('profile-form');
    const profileMessage = document.getElementById('profile-message');
    const changePasswordForm = document.getElementById('change-password-form');
    const changePasswordMessage = document.getElementById('change-password-message');
    const toggleUserActiveButton = document.getElementById('toggle-user-active');

    let sessionUser = null;
    let selectedUser = null;
    let userPage = 1;
    let userTotalPages = 1;
    let userSearchTimer = null;
    const userPageSize = 10;

    function setBusy(button, busy, busyText) {
        if (!button) return;

        if (busy) {
            button.dataset.originalText = button.textContent;
            button.textContent = busyText;
            button.disabled = true;
            return;
        }

        button.textContent = button.dataset.originalText || button.textContent;
        button.disabled = false;
        delete button.dataset.originalText;
    }

    function setMessage(element, message, success = false) {
        element.textContent = message;
        element.classList.toggle('success', success);
    }

    function notify(message) {
        if (typeof showToast === 'function') {
            showToast(message);
        }
    }

    async function readError(response, fallback) {
        if (typeof readApiError === 'function') {
            return await readApiError(response, fallback);
        }

        return fallback;
    }

    function formatUserDate(value) {
        if (!value) return '—';

        return new Intl.DateTimeFormat('tr-TR', {
            dateStyle: 'medium'
        }).format(new Date(value));
    }

    function createAccountState(isActive) {
        const badge = document.createElement('span');
        badge.className = `account-state ${isActive ? 'active' : 'inactive'}`;
        badge.textContent = isActive ? 'Aktif' : 'Onay bekliyor / pasif';
        return badge;
    }

    function createRoleBadge(role) {
        const badge = document.createElement('span');
        badge.className = 'role-badge';
        badge.textContent = roleLabels[role] || role;
        return badge;
    }

    function handleUnauthorized(response) {
        if (response.status !== 401) return false;

        showLogin('Oturumun sona erdi. Lütfen yeniden giriş yap.');
        return true;
    }

    async function loadUsers() {
        if (sessionUser?.role !== 'Admin') return false;

        const parameters = new URLSearchParams({
            page: String(userPage),
            pageSize: String(userPageSize)
        });

        const search = userSearch.value.trim();
        const role = userRoleFilter.value;
        const isActive = userActiveFilter.value;

        if (search) parameters.set('search', search);
        if (role) parameters.set('role', role);
        if (isActive) parameters.set('isActive', isActive);

        try {
            const response = await fetch(`/api/users/paged?${parameters}`);

            if (handleUnauthorized(response)) return false;

            if (response.status === 403) {
                notify('Kullanıcı yönetimi için yönetici yetkisi gerekli.');
                return false;
            }

            if (!response.ok) {
                notify(await readError(response, 'Kullanıcılar yüklenemedi.'));
                return false;
            }

            const result = await response.json();
            userTotalPages = Math.max(1, result.totalPages || 0);

            if (userPage > userTotalPages) {
                userPage = userTotalPages;
                return await loadUsers();
            }

            renderUsers(result);
            return true;
        } catch {
            notify('Kullanıcılar yüklenirken sunucuya ulaşılamadı.');
            return false;
        }
    }

    function renderUsers(result) {
        userTable.replaceChildren();

        if (!result.items?.length) {
            const row = document.createElement('tr');
            const cell = document.createElement('td');
            cell.colSpan = 6;
            cell.className = 'empty-state';
            cell.textContent = 'Filtrelere uygun kullanıcı bulunamadı.';
            row.append(cell);
            userTable.append(row);
        } else {
            result.items.forEach(user => {
                const row = document.createElement('tr');

                const identityCell = document.createElement('td');
                identityCell.className = 'user-cell';
                const fullName = document.createElement('strong');
                fullName.textContent = user.fullName;
                const username = document.createElement('small');
                username.textContent = `@${user.username}`;
                identityCell.append(fullName, username);

                const emailCell = document.createElement('td');
                emailCell.textContent = user.email;

                const roleCell = document.createElement('td');
                roleCell.append(createRoleBadge(user.role));

                const stateCell = document.createElement('td');
                stateCell.append(createAccountState(user.isActive));

                const createdCell = document.createElement('td');
                createdCell.textContent = formatUserDate(user.createdAt);

                const actionCell = document.createElement('td');
                const actionButton = document.createElement('button');
                actionButton.type = 'button';
                actionButton.className = 'row-action';
                actionButton.textContent = '→';
                actionButton.title = 'Kullanıcı detayını aç';
                actionButton.setAttribute('aria-label', `${user.fullName} kullanıcısının detayını aç`);
                actionButton.addEventListener('click', () => showUserDetail(user.id));
                actionCell.append(actionButton);

                row.append(identityCell, emailCell, roleCell, stateCell, createdCell, actionCell);
                userTable.append(row);
            });
        }

        document.getElementById('user-summary').textContent = `${result.totalCount} kullanıcı listeleniyor`;
        document.getElementById('user-page-info').textContent = `${userPage} / ${userTotalPages}`;
        document.getElementById('user-previous-page').disabled = userPage === 1;
        document.getElementById('user-next-page').disabled = userPage === userTotalPages;
    }

    async function showUserDetail(userId) {
        setMessage(userEditMessage, '');

        try {
            const response = await fetch(`/api/users/${userId}`);

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                notify(await readError(response, 'Kullanıcı detayı yüklenemedi.'));
                return;
            }

            selectedUser = await response.json();
            document.getElementById('user-detail-username').textContent = `@${selectedUser.username}`;
            document.getElementById('user-created-sample-count').textContent = selectedUser.createdSampleCount;
            document.getElementById('user-assigned-sample-count').textContent = selectedUser.assignedSampleCount;
            document.getElementById('user-detail-state').replaceChildren(createAccountState(selectedUser.isActive));
            document.getElementById('user-edit-full-name').value = selectedUser.fullName;
            document.getElementById('user-edit-email').value = selectedUser.email;
            document.getElementById('user-edit-role').value = selectedUser.role;

            toggleUserActiveButton.textContent = selectedUser.isActive
                ? 'Hesabı pasifleştir'
                : 'Hesabı aktifleştir';
            toggleUserActiveButton.classList.toggle('button-danger', selectedUser.isActive);
            toggleUserActiveButton.classList.toggle('button-secondary', !selectedUser.isActive);
            toggleUserActiveButton.disabled = selectedUser.id === sessionUser?.id && selectedUser.isActive;
            toggleUserActiveButton.title = toggleUserActiveButton.disabled
                ? 'Kendi hesabını pasifleştiremezsin.'
                : '';

            openModal(userDetailView);
        } catch {
            notify('Kullanıcı detayı yüklenirken sunucuya ulaşılamadı.');
        }
    }

    async function loadProfile() {
        setMessage(profileMessage, '');

        try {
            const response = await fetch('/api/profile');

            if (handleUnauthorized(response)) return false;

            if (!response.ok) {
                setMessage(profileMessage, await readError(response, 'Profil bilgileri yüklenemedi.'));
                return false;
            }

            const profile = await response.json();
            document.getElementById('profile-display-name').textContent = profile.fullName;
            document.getElementById('profile-username').textContent = `@${profile.username}`;
            document.getElementById('profile-avatar').textContent = profile.fullName.slice(0, 1).toUpperCase();
            document.getElementById('profile-role').textContent = roleLabels[profile.role] || profile.role;
            document.getElementById('profile-state').replaceChildren(createAccountState(profile.isActive));
            document.getElementById('profile-full-name').value = profile.fullName;
            document.getElementById('profile-email').value = profile.email;
            return true;
        } catch {
            setMessage(profileMessage, 'Profil yüklenirken sunucuya ulaşılamadı.');
            return false;
        }
    }

    document.getElementById('show-register').addEventListener('click', () => {
        registerForm.reset();
        setMessage(registerMessage, '');
        openModal(registerView);
    });

    registerForm.addEventListener('submit', async event => {
        event.preventDefault();
        setMessage(registerMessage, '');

        const form = new FormData(registerForm);
        const password = form.get('password');
        const confirmPassword = form.get('confirmPassword');

        if (password !== confirmPassword) {
            setMessage(registerMessage, 'Parola ve parola tekrarı aynı olmalıdır.');
            return;
        }

        const submitButton = registerForm.querySelector('button[type="submit"]');
        setBusy(submitButton, true, 'Gönderiliyor...');

        try {
            const response = await fetch('/api/auth/register', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    username: form.get('username'),
                    fullName: form.get('fullName'),
                    email: form.get('email'),
                    password,
                    confirmPassword
                })
            });

            if (!response.ok) {
                setMessage(registerMessage, await readError(response, 'Kayıt başvurusu oluşturulamadı.'));
                return;
            }

            registerForm.reset();
            setMessage(registerMessage, 'Başvurun alındı. Yönetici hesabını aktifleştirdikten sonra giriş yapabilirsin.', true);
        } catch {
            setMessage(registerMessage, 'Kayıt sırasında sunucuya ulaşılamadı.');
        } finally {
            setBusy(submitButton, false);
        }
    });

    document.getElementById('show-create-user').addEventListener('click', () => {
        createUserForm.reset();
        setMessage(createUserMessage, '');
        openModal(createUserView);
    });

    createUserForm.addEventListener('submit', async event => {
        event.preventDefault();
        setMessage(createUserMessage, '');
        const form = new FormData(createUserForm);
        const submitButton = createUserForm.querySelector('button[type="submit"]');
        setBusy(submitButton, true, 'Oluşturuluyor...');

        try {
            const response = await fetch('/api/users', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    username: form.get('username'),
                    fullName: form.get('fullName'),
                    email: form.get('email'),
                    role: form.get('role'),
                    temporaryPassword: form.get('temporaryPassword')
                })
            });

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                setMessage(createUserMessage, await readError(response, 'Kullanıcı oluşturulamadı.'));
                return;
            }

            closeModal(createUserView);
            userPage = 1;
            await loadUsers();
            notify('Kullanıcı başarıyla oluşturuldu.');
        } catch {
            setMessage(createUserMessage, 'Kullanıcı oluşturulurken sunucuya ulaşılamadı.');
        } finally {
            setBusy(submitButton, false);
        }
    });

    userEditForm.addEventListener('submit', async event => {
        event.preventDefault();
        if (!selectedUser) return;

        setMessage(userEditMessage, '');
        const form = new FormData(userEditForm);
        const submitButton = userEditForm.querySelector('button[type="submit"]');
        setBusy(submitButton, true, 'Kaydediliyor...');

        try {
            const response = await fetch(`/api/users/${selectedUser.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullName: form.get('fullName'),
                    email: form.get('email'),
                    role: form.get('role')
                })
            });

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                setMessage(userEditMessage, await readError(response, 'Kullanıcı güncellenemedi.'));
                return;
            }

            await loadUsers();
            await showUserDetail(selectedUser.id);
            notify('Kullanıcı bilgileri güncellendi.');
        } catch {
            setMessage(userEditMessage, 'Kullanıcı güncellenirken sunucuya ulaşılamadı.');
        } finally {
            setBusy(submitButton, false);
        }
    });

    toggleUserActiveButton.addEventListener('click', async () => {
        if (!selectedUser || toggleUserActiveButton.disabled) return;

        setMessage(userEditMessage, '');
        const nextActiveState = !selectedUser.isActive;
        setBusy(
            toggleUserActiveButton,
            true,
            nextActiveState ? 'Aktifleştiriliyor...' : 'Pasifleştiriliyor...');

        try {
            const response = await fetch(`/api/users/${selectedUser.id}/active`, {
                method: 'PATCH',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ isActive: nextActiveState })
            });

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                setMessage(userEditMessage, await readError(response, 'Hesap durumu değiştirilemedi.'));
                return;
            }

            setBusy(toggleUserActiveButton, false);
            await loadUsers();
            await showUserDetail(selectedUser.id);
            notify(nextActiveState ? 'Kullanıcı aktifleştirildi.' : 'Kullanıcı pasifleştirildi.');
        } catch {
            setMessage(userEditMessage, 'Hesap durumu değiştirilirken sunucuya ulaşılamadı.');
        } finally {
            setBusy(toggleUserActiveButton, false);
        }
    });

    profileForm.addEventListener('submit', async event => {
        event.preventDefault();
        setMessage(profileMessage, '');
        const form = new FormData(profileForm);
        const submitButton = profileForm.querySelector('button[type="submit"]');
        setBusy(submitButton, true, 'Kaydediliyor...');

        try {
            const response = await fetch('/api/profile', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullName: form.get('fullName'),
                    email: form.get('email')
                })
            });

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                setMessage(profileMessage, await readError(response, 'Profil güncellenemedi.'));
                return;
            }

            await loadProfile();
            setMessage(profileMessage, 'Profil bilgilerin güncellendi.', true);
            notify('Profil güncellendi.');
        } catch {
            setMessage(profileMessage, 'Profil güncellenirken sunucuya ulaşılamadı.');
        } finally {
            setBusy(submitButton, false);
        }
    });

    changePasswordForm.addEventListener('submit', async event => {
        event.preventDefault();
        setMessage(changePasswordMessage, '');

        const form = new FormData(changePasswordForm);
        const newPassword = form.get('newPassword');
        const confirmNewPassword = form.get('confirmNewPassword');

        if (newPassword !== confirmNewPassword) {
            setMessage(changePasswordMessage, 'Yeni parola ve parola tekrarı aynı olmalıdır.');
            return;
        }

        const submitButton = changePasswordForm.querySelector('button[type="submit"]');
        setBusy(submitButton, true, 'Değiştiriliyor...');

        try {
            const response = await fetch('/api/profile/change-password', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    currentPassword: form.get('currentPassword'),
                    newPassword,
                    confirmNewPassword
                })
            });

            if (handleUnauthorized(response)) return;

            if (!response.ok) {
                setMessage(changePasswordMessage, await readError(response, 'Parola değiştirilemedi.'));
                return;
            }

            changePasswordForm.reset();
            setMessage(changePasswordMessage, 'Parolan başarıyla değiştirildi.', true);
            notify('Parola değiştirildi.');
        } catch {
            setMessage(changePasswordMessage, 'Parola değiştirilirken sunucuya ulaşılamadı.');
        } finally {
            setBusy(submitButton, false);
        }
    });

    userSearch.addEventListener('input', () => {
        clearTimeout(userSearchTimer);
        userSearchTimer = setTimeout(() => {
            userPage = 1;
            loadUsers();
        }, 350);
    });

    userRoleFilter.addEventListener('change', () => {
        userPage = 1;
        loadUsers();
    });

    userActiveFilter.addEventListener('change', () => {
        userPage = 1;
        loadUsers();
    });

    document.getElementById('user-previous-page').addEventListener('click', () => {
        if (userPage <= 1) return;
        userPage -= 1;
        loadUsers();
    });

    document.getElementById('user-next-page').addEventListener('click', () => {
        if (userPage >= userTotalPages) return;
        userPage += 1;
        loadUsers();
    });

    globalThis.UserManagement = {
        async onSessionStarted(user, initialView) {
            sessionUser = user;

            if (initialView === 'profile-view') {
                await loadProfile();
            }
        },

        onSessionEnded() {
            sessionUser = null;
            selectedUser = null;
            userTable.replaceChildren();
        },

        onViewChanged(viewId) {
            if (!sessionUser) return;

            if (viewId === 'users-view') {
                loadUsers();
            } else if (viewId === 'profile-view') {
                loadProfile();
            }
        }
    };
})();
