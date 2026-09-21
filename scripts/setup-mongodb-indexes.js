use('dotCheck');

db.users.createIndex({ userName: 1 }, { unique: true, name: 'ux_userName' });
db.items.createIndex({ status: 1, createdAt: -1 }, { name: 'ix_status_createdAt' });
db.items.createIndex({ ownerUserId: 1, createdAt: -1 }, { name: 'ix_owner_createdAt' });
db.uncheckedReasons.createIndex({ userId: 1, updatedAt: -1 }, { name: 'ix_user_updatedAt' });
db.uncheckedReasons.createIndex({ itemId: 1, userId: 1 }, { unique: true, name: 'ux_item_user' });
