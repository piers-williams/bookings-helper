# OSM API Discovery

**Status:** Pending Manual Exploration

## Instructions

To complete this task, you need to reverse engineer the OSM (Online Scout Manager) API by observing network traffic in your browser:

### Steps:

1. **Open OSM in Browser**
   - Navigate to: https://www.onlinescoutmanager.co.uk
   - Log in with your credentials

2. **Open Browser DevTools**
   - Press F12 (Chrome/Edge) or Cmd+Option+I (Mac)
   - Go to the "Network" tab
   - Enable "Preserve log" checkbox

3. **Perform Actions and Document API Calls**
   - Navigate to bookings section
   - View a booking
   - Read comments
   - Note all API requests made

4. **Document Findings Below**

---

## Authentication

OSM uses OAuth 2.0 for authorisation. OAuth is a standard mechanism used by software around the world that allows users to enter their password in one system without having to give it to the third party software.

The best way to use this is to download an OAuth client library for the programming language you are using and follow its documentation with the following URLs and scopes.

The 'authorization code flow' should be used if your application will be used by other people. If you are the only user of the application then you can use the 'client credentials flow' to use the application with the user account that created the application.

### Authorisation
https://www.onlinescoutmanager.co.uk/oauth/authorize
If you are using the 'authorization code flow', your client library will build a link based on this URL that your users will click - this will bring them to OSM where they will be asked to log in. If they log in and authorise your application, they will be redirected back to the Redirect URL you specify.

### Access token
https://www.onlinescoutmanager.co.uk/oauth/token
When the user has been redirected back to your application, your client library will make a request to this URL to get an 'access token' and a 'refresh token' - these should be stored in your database.

### Resource owner
https://www.onlinescoutmanager.co.uk/oauth/resource
This will provide you with the user's full name, email address, and a list of sections that your application can access.

### Scopes
Prefix each of the following scopes with 'section:' and add the suffix of ':read' or ':write' to determine if your application needs read access (:read) or read and write access (:write).

The 'administration' and 'finance' scopes have an additional suffix of ':admin' which is used for editing critical settings.

Please ask for the lowest possible set of permissions as you will not be able to see sections unless the user has all the permissions your application specifies.

administration - Administration / settings areas.

campsite_setup - Venue Setup

campsite_bookings - Venue Bookings

member - Personal details, including adding/removing/transferring, emailing, obtaining contact details etc.

Your browser uses a simple API for interacting with OSM, so although our API is unsupported and undocumented, your application will be able to do anything that you can do on the website.

You should use your browser's developer console to watch the network requests that are made when you perform the actions you would like your application to automate. Pay particular attention to whether requests are GET or POST. Then use your OAuth client library to create an authenticated request to the URL you discovered via the console (which will set a 'Bearer token' in the authorisation header).

Please monitor the standard rate limit headers to ensure your application does not get blocked automatically. Applications that are frequently blocked will be permanently blocked.

X-RateLimit-Limit - this is the number of requests per hour that your API can perform (per authenticated user)

X-RateLimit-Remaining - this is the number of requests remaining that the current user can perform before they are blocked

X-RateLimit-Reset - this is the number of seconds until the rate limit for the current user resets back to your overall limit

An HTTP 429 status code will be sent if the user goes over the limit, along with a Retry-After header with the number of seconds until you can use the API again.

Please also enforce your own lower rate limits, especially if you are allowing unauthenticated users to manipulate your data (e.g. allowing members to join a waiting list).

## Endpoints Discovered

### GET /api/bookings (or similar)
**Purpose:** List bookings

**URL:** `https://www.onlinescoutmanager.co.uk/v3/campsites/219/bookings`

**Query Parameters:**
- `mode`: [required] - Filter by status (`provisional`, `current`, `future`, `past`, `cancelled`)

**Request Headers:**
```
accept `application/json, text/plain, */*`
accept-encoding `gzip, deflate, br, zstd`
accept-language `en-GB,en-US;q=0.9,en;q=0.8`
```

**Sample Request:**
```bash
curl 'https://www.onlinescoutmanager.co.uk/api/...' \
  -H 'Authorization: ...' \
  -H 'Cookie: ...'
```

**Response Format:**
```json
{
    "status": true,
    "error": null,
    "data": [
        {
            "id": 150704,
            "campsite_site_id": 219,
            "campsite_organisation_id": 609,
            "member_id": 2452271,
            "group_name": "x",
            "start_date": "2026-01-30",
            "end_date": "2026-02-01",
            "number_leaders": 6,
            "number_participants": 40,
            "status": "confirmed",
            "deposit": 150,
            "deposit_due": "2025-10-25 00:00:00",
            "total_price": 750,
            "total_paid": 150,
            "card_payments_config": null,
            "campsite_users_card_account_id": 0
        },
        {
            "id": 153277,
            "campsite_site_id": 219,
            "campsite_organisation_id": 609,
            "member_id": 3260538,
            "group_name": "x",
            "start_date": "2026-01-30",
            "end_date": "2026-02-01",
            "number_leaders": 3,
            "number_participants": 13,
            "status": "confirmed",
            "deposit": 104,
            "deposit_due": "2025-11-23 00:00:00",
            "total_price": 520,
            "total_paid": 104,
            "card_payments_config": null,
            "campsite_users_card_account_id": 0
        }
    ],
    "meta": []
}
```

---

### GET /api/bookings/{id} (or similar)
**Purpose:** Get booking details

**URL:** `https://www.onlinescoutmanager.co.uk/v3/campsites/219/items?booking_id=153277&mode=booking&audience=venue`

**Path Parameters:**
- `booking_id`: Booking ID
- `mode`: Always `booking`
- `audience`: Always `venue`

**Response Format:**
```json
{
    "status": true,
    "error": null,
    "data": [
        {
            "id": 3868,
            "parent_id": 1384,
            "campsite_session_id": 0,
            "campsite_price_type_id": 0,
            "availability_rules": [
                998
            ],
            "also_check": null,
            "name": "Campsites",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 0,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": null,
            "price": null,
            "bookings": [],
            "pictures": []
        },
        {
            "id": 3867,
            "parent_id": 1384,
            "campsite_session_id": 0,
            "campsite_price_type_id": 0,
            "availability_rules": [
                998
            ],
            "also_check": null,
            "name": "Indoor Accommodation",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 0,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": null,
            "price": null,
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1384,
            "parent_id": 0,
            "campsite_session_id": 733,
            "campsite_price_type_id": 807,
            "availability_rules": [
                848,
                998
            ],
            "also_check": [
                1385
            ],
            "name": "Whole Site Booking",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 733,
                "name": "AA - Whole Site Booking"
            },
            "price": {
                "id": 807,
                "name": "Whole Site Booking"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1386,
            "parent_id": 3867,
            "campsite_session_id": 728,
            "campsite_price_type_id": 800,
            "availability_rules": [],
            "also_check": [],
            "name": "Alpha House",
            "description": "Alpha House is our largest building that can provide sleeping accommodation for 48 individuals in its upstairs dormitories, as well as providing a spacious area for indoor activities and dining.\n\n**PLEASE SEE ATTACHED FLOOR PLAN FOR SLEEPING ARRANGEMENTS**",
            "terms_and_conditions": "",
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 48,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 728,
                "name": "Accommodation"
            },
            "price": {
                "id": 800,
                "name": "Alpha House"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34134,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Rear%20View%20-%20Alpha%20House_edited.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 1467198,
                    "date_uploaded": "2021-10-28 12:01:48",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FRear%2520View%2520-%2520Alpha%2520House_edited.jpg&id=1386&fm=png&fit=crop&w=100&h=75&cb=b146b2a501dc8a0a611a231bbd54c845&s=8baea97d769fffe7cb5f741ad42c31f5",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FRear%2520View%2520-%2520Alpha%2520House_edited.jpg&id=1386&fm=png&fit=contain&w=565&h=424&cb=b146b2a501dc8a0a611a231bbd54c845&s=eea6a8cc49f8d488ca8ed80c15d9a4cf"
                },
                {
                    "upload_id": 34145,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Communal Area - Alpha House.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 2210468,
                    "date_uploaded": "2021-10-28 12:11:16",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FCommunal+Area+-+Alpha+House.jpg&id=1386&fm=png&fit=crop&w=100&h=75&cb=b7f20edaeddf2c4fd6adc999c037f959&s=0131da8ec738f2c63c4510fbfb314f24",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FCommunal+Area+-+Alpha+House.jpg&id=1386&fm=png&fit=contain&w=565&h=424&cb=b7f20edaeddf2c4fd6adc999c037f959&s=6e7e932f5b94e84f0c26526b24129394"
                },
                {
                    "upload_id": 34146,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Ron Sibley Room - Alpha House.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 2209939,
                    "date_uploaded": "2021-10-28 12:11:16",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FRon+Sibley+Room+-+Alpha+House.jpg&id=1386&fm=png&fit=crop&w=100&h=75&cb=e04247f27a3366aee72fd5932b9308df&s=f14d98dd628e7b8408cef83bc2f278fb",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FRon+Sibley+Room+-+Alpha+House.jpg&id=1386&fm=png&fit=contain&w=565&h=424&cb=e04247f27a3366aee72fd5932b9308df&s=01720e1004cc82449a51b26a5cf7cdc8"
                },
                {
                    "upload_id": 34147,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Kitchen - Alpha House.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 2523034,
                    "date_uploaded": "2021-10-28 12:11:16",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FKitchen+-+Alpha+House.jpg&id=1386&fm=png&fit=crop&w=100&h=75&cb=a8c0307fa6e6a63d4991f5bdd1da5fa9&s=cb00c5734909b57fea6ae420b93561b9",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FKitchen+-+Alpha+House.jpg&id=1386&fm=png&fit=contain&w=565&h=424&cb=a8c0307fa6e6a63d4991f5bdd1da5fa9&s=0099ab551ed942e1baac217f1f5aafbf"
                },
                {
                    "upload_id": 34148,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "AlphaHouseFP.png",
                    "temp": "no",
                    "mime": "image\/png",
                    "extension": "png",
                    "size": 702987,
                    "date_uploaded": "2021-10-28 12:11:22",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FAlphaHouseFP.png&id=1386&fm=png&fit=crop&w=100&h=75&cb=b9d06380dcde0eff5efe11f29251a43e&s=09785c0eff112508a1b0e42e6899c5c4",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FAlphaHouseFP.png&id=1386&fm=png&fit=contain&w=565&h=424&cb=b9d06380dcde0eff5efe11f29251a43e&s=62a16268c826361c7d564a59dbf2df42"
                }
            ]
        },
        {
            "id": 1387,
            "parent_id": 3867,
            "campsite_session_id": 728,
            "campsite_price_type_id": 802,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Hayvern",
            "description": "Hayvern is our medium-sized building and provides sleeping accommodation for up to 38 individuals within its 5 sleeping areas. There are 3 bedrooms with 2 bunk beds, 1 bedroom with a bunk bed, and then a large dormitory with 12 bunk beds.\n\n**PLEASE SEE ATTACHED FLOOR PLAN FOR SLEEPING ARRANGEMENTS**",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 38,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 728,
                "name": "Accommodation"
            },
            "price": {
                "id": 802,
                "name": "Hayvern"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34136,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "IMG_1824.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 4751004,
                    "date_uploaded": "2021-10-28 12:02:18",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FIMG_1824.jpg&id=1387&fm=png&fit=crop&w=100&h=75&cb=e46d6cc620ce2a21962e516f5eb6b193&s=6c972dfa48a43f38b7386e49e6ebd3b4",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FIMG_1824.jpg&id=1387&fm=png&fit=contain&w=565&h=424&cb=e46d6cc620ce2a21962e516f5eb6b193&s=bfebc2f24d8454334739fd0e8bf06d42"
                },
                {
                    "upload_id": 34142,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "HayvernFP.png",
                    "temp": "no",
                    "mime": "image\/png",
                    "extension": "png",
                    "size": 428206,
                    "date_uploaded": "2021-10-28 12:09:41",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHayvernFP.png&id=1387&fm=png&fit=crop&w=100&h=75&cb=f0b3a0e6b61d46271e610b3a9b825b83&s=14181e0de674caf68d975b7f562a895a",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHayvernFP.png&id=1387&fm=png&fit=contain&w=565&h=424&cb=f0b3a0e6b61d46271e610b3a9b825b83&s=3d1f1d39cced9c5128f2a1352f4a7442"
                }
            ]
        },
        {
            "id": 1388,
            "parent_id": 3867,
            "campsite_session_id": 728,
            "campsite_price_type_id": 803,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Papillon",
            "description": "Despite being our smallest building, Papillon provides sleeping accommodation for 14 individuals, it consists of 2 bedrooms with a bunk bed, 1 bedroom with 3 bunk beds and a fourth bedroom containing 2 bunk beds.\n\n**PLEASE SEE ATTACHED FLOOR PLAN FOR SLEEPING ARRANGEMENTS**",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 14,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 728,
                "name": "Accommodation"
            },
            "price": {
                "id": 803,
                "name": "Papillon"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34140,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Hayvern.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 5427623,
                    "date_uploaded": "2021-10-28 12:03:10",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHayvern.jpg&id=1388&fm=png&fit=crop&w=100&h=75&cb=a8511dbf0801d5d8719fc6090a25e29d&s=39196b78e14f41792791d0e907084d55",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHayvern.jpg&id=1388&fm=png&fit=contain&w=565&h=424&cb=a8511dbf0801d5d8719fc6090a25e29d&s=c7faecdf50223dc3fe19c3a154fdaf03"
                },
                {
                    "upload_id": 34143,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Communal area .jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 2531870,
                    "date_uploaded": "2021-10-28 12:10:13",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FCommunal+area+.jpg&id=1388&fm=png&fit=crop&w=100&h=75&cb=297737c30ff2dac6d97d7d83edd10483&s=d2eba3296036831ffcb58141cdc8d6ac",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FCommunal+area+.jpg&id=1388&fm=png&fit=contain&w=565&h=424&cb=297737c30ff2dac6d97d7d83edd10483&s=1857d2666eae3bb6a888e7e865cd4367"
                },
                {
                    "upload_id": 34144,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "PapillonFP.png",
                    "temp": "no",
                    "mime": "image\/png",
                    "extension": "png",
                    "size": 381837,
                    "date_uploaded": "2021-10-28 12:10:27",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FPapillonFP.png&id=1388&fm=png&fit=crop&w=100&h=75&cb=ecc6da4652f16b7271ef14cf858e76a8&s=157785183dc951929fe5b2996f6a562d",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FPapillonFP.png&id=1388&fm=png&fit=contain&w=565&h=424&cb=ecc6da4652f16b7271ef14cf858e76a8&s=95454bf2ceda5c24627e126f5beab601"
                }
            ]
        },
        {
            "id": 3864,
            "parent_id": 3867,
            "campsite_session_id": 728,
            "campsite_price_type_id": 1976,
            "availability_rules": null,
            "also_check": null,
            "name": "Winter Fuel Surcharge",
            "description": "Mandatory charge during winter months due to increased utilities use.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 10,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 728,
                "name": "Accommodation"
            },
            "price": {
                "id": 1976,
                "name": "Winter Fuel Surcharge"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 5174,
            "parent_id": 3868,
            "campsite_session_id": 2167,
            "campsite_price_type_id": 805,
            "availability_rules": null,
            "also_check": [
                1384
            ],
            "name": "Campfire Circle",
            "description": null,
            "terms_and_conditions": "",
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2167,
                "name": "CampfireCircle"
            },
            "price": {
                "id": 805,
                "name": "Campsites (Evening Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1404,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Birch",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1412,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 805,
            "availability_rules": [
                848
            ],
            "also_check": null,
            "name": "Evening Visit",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 10,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 805,
                "name": "Campsites (Evening Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1403,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 806,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Fallen Log",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 80,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 806,
                "name": "Campsites (Overnight Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1407,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                933
            ],
            "also_check": [],
            "name": "Fir & Elm",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 80,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1402,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Greens",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1405,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Gum",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1406,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Headquarters (HQ)",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 80,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34128,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "HQ.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 5627876,
                    "date_uploaded": "2021-10-28 11:58:49",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHQ.jpg&id=1406&fm=png&fit=crop&w=100&h=75&cb=177b6e479cd1640a996743964d88bf97&s=ed9f4f2a044e1da500857d74cc4a4764",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHQ.jpg&id=1406&fm=png&fit=contain&w=565&h=424&cb=177b6e479cd1640a996743964d88bf97&s=15f7b97ff0a1c4193d5014b87e5a21eb"
                }
            ]
        },
        {
            "id": 1401,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Hill",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34127,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Hill.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 6061459,
                    "date_uploaded": "2021-10-28 11:57:30",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHill.jpg&id=1401&fm=png&fit=crop&w=100&h=75&cb=36bc07e2ee39f90f0e9b7159deb0f767&s=e89a2dc642ece0cb9ef94f9623bd264b",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHill.jpg&id=1401&fm=png&fit=contain&w=565&h=424&cb=36bc07e2ee39f90f0e9b7159deb0f767&s=d3ee79bc8dfcf60db46838ea161d30ca"
                }
            ]
        },
        {
            "id": 1399,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Hurricane",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34130,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Hurricane.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 5873494,
                    "date_uploaded": "2021-10-28 11:59:22",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHurricane.jpg&id=1399&fm=png&fit=crop&w=100&h=75&cb=396f6bff643664ec08b66643e0fcee36&s=16286655a3f3abe156074996d6677333",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FHurricane.jpg&id=1399&fm=png&fit=contain&w=565&h=424&cb=396f6bff643664ec08b66643e0fcee36&s=286fe30beffdca63ab816c88bc6c128e"
                }
            ]
        },
        {
            "id": 1396,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Moules",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1398,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Oak",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 15,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1400,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Spring",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 36,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34126,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Spring.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 6243350,
                    "date_uploaded": "2021-10-28 11:57:16",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FSpring.jpg&id=1400&fm=png&fit=crop&w=100&h=75&cb=e6b9fa43b6e228f6a72b5e529c7ef59d&s=7920e584fb4db6e10f5d7e8b5e6523d5",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FSpring.jpg&id=1400&fm=png&fit=contain&w=565&h=424&cb=e6b9fa43b6e228f6a72b5e529c7ef59d&s=c39e705bfae0f7c5b6f064d0827e8fbf"
                }
            ]
        },
        {
            "id": 1408,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Tim's Field",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 50,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34131,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "BottomFieldMOWN_edited.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 2890834,
                    "date_uploaded": "2021-10-28 11:59:33",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FBottomFieldMOWN_edited.jpg&id=1408&fm=png&fit=crop&w=100&h=75&cb=54a8b6640e7fc78ff18b9db390c09cbc&s=a27f57bf906c7bed116fa9dd9e28b897",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FBottomFieldMOWN_edited.jpg&id=1408&fm=png&fit=contain&w=565&h=424&cb=54a8b6640e7fc78ff18b9db390c09cbc&s=26d5dcacbc97cff877f81d25475c45c5"
                }
            ]
        },
        {
            "id": 1397,
            "parent_id": 3868,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": [],
            "name": "Top Field",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 80,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": [
                {
                    "upload_id": 34129,
                    "uploader_id": "scouts:417898",
                    "section_id": 56710,
                    "public_directory": "\/",
                    "public_name": "Top Field.jpg",
                    "temp": "no",
                    "mime": "image\/jpeg",
                    "extension": "jpg",
                    "size": 5939402,
                    "date_uploaded": "2021-10-28 11:59:09",
                    "deleted_at": null,
                    "thumbnail_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FTop+Field.jpg&id=1397&fm=png&fit=crop&w=100&h=75&cb=cd5234ffede166e466dbc0d136b52c23&s=fe7db93cdecfb215a901e358ec4e6666",
                    "preview_url": "\/ext\/campsites\/items\/pictures\/index.php?section_id=56710&action=preview&file=%2FTop+Field.jpg&id=1397&fm=png&fit=contain&w=565&h=424&cb=cd5234ffede166e466dbc0d136b52c23&s=61939acbecc2456f23fdb81cfa959105"
                }
            ]
        },
        {
            "id": 1544,
            "parent_id": 0,
            "campsite_session_id": 729,
            "campsite_price_type_id": 804,
            "availability_rules": [
                848
            ],
            "also_check": null,
            "name": "ZZ - Day Visit (FOR CAMPSITE ONLY WHEN WSB IN-SITU).",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 2,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 804,
                "name": "Campsites (Day Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1545,
            "parent_id": 0,
            "campsite_session_id": 729,
            "campsite_price_type_id": 806,
            "availability_rules": [
                848
            ],
            "also_check": null,
            "name": "ZZ - Overnight Visit (FOR CAMPSITE ONLY WHEN WSB IN-SITU).",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 2,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 729,
                "name": "Camping"
            },
            "price": {
                "id": 806,
                "name": "Campsites (Overnight Visit)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 3824,
            "parent_id": 1386,
            "campsite_session_id": 1685,
            "campsite_price_type_id": 1958,
            "availability_rules": [
                859,
                860
            ],
            "also_check": null,
            "name": "ZZ - Midweek Training",
            "description": "Please note this is only available during term-time and is for external organisations holding training. This will be unavailable if Alpha House is booked and vice versa.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 48,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 1685,
                "name": "Midweek Training"
            },
            "price": {
                "id": 1958,
                "name": "Midweek Training"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 1546,
            "parent_id": 0,
            "campsite_session_id": 1678,
            "campsite_price_type_id": 1943,
            "availability_rules": [
                848
            ],
            "also_check": [
                4961
            ],
            "name": "ZZ - Wet Weather",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 25,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": false,
            "session": {
                "id": 1678,
                "name": "Wet Weather"
            },
            "price": {
                "id": 1943,
                "name": "Wet Weather (Half Day)"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 6869,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 3561,
            "availability_rules": [],
            "also_check": null,
            "name": "ADD-ON - Fridge No. 1",
            "description": "This is an additional fridge for hire that is located to the rear of the site providore.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 3561,
                "name": "\u00a34 Fridges"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 6870,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 3561,
            "availability_rules": null,
            "also_check": null,
            "name": "ADD-ON - Fridge No. 2",
            "description": "This is an additional fridge for hire that is located to the rear of the site providore.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 3561,
                "name": "\u00a34 Fridges"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 6871,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 3561,
            "availability_rules": null,
            "also_check": null,
            "name": "ADD-ON - Fridge No. 3",
            "description": "This is an additional fridge for hire that is located to the rear of the site providore.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 3561,
                "name": "\u00a34 Fridges"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 8053,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 3561,
            "availability_rules": null,
            "also_check": null,
            "name": "ADD-ON - Fridge No. 4",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 3561,
                "name": "\u00a34 Fridges"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 6879,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 2487,
            "availability_rules": [
                933,
                535
            ],
            "also_check": null,
            "name": "ADD-ON - Hammock Hire",
            "description": "Hire includes 12 hammocks and 1 tent, giving your group the chance to experience a unique style of camping under the trees. Perfect for backwoods skills, patrol camps, or lightweight expeditions.",
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 12,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 7798,
            "parent_id": 0,
            "campsite_session_id": 4020,
            "campsite_price_type_id": 4042,
            "availability_rules": null,
            "also_check": null,
            "name": "ADD-ON - Midweek Check In",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 461,
            "number_instructors": 1,
            "number_participants": 0,
            "minimum_instructors": 1,
            "available": true,
            "session": {
                "id": 4020,
                "name": "XX Accommodation ADD-ON"
            },
            "price": {
                "id": 4042,
                "name": "\u00a30 Add On"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 5401,
            "parent_id": 0,
            "campsite_session_id": 2101,
            "campsite_price_type_id": 2769,
            "availability_rules": null,
            "also_check": [
                4963
            ],
            "name": "ACTIVITY - Crystal Maze",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2101,
                "name": "XX Activity - 2HR Blocks CJ"
            },
            "price": {
                "id": 2769,
                "name": "\u00a325 Activities - 2HRS"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4972,
            "parent_id": 0,
            "campsite_session_id": 2101,
            "campsite_price_type_id": 2492,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Pond Dipping",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 1,
            "available": true,
            "session": {
                "id": 2101,
                "name": "XX Activity - 2HR Blocks CJ"
            },
            "price": {
                "id": 2492,
                "name": "\u00a315 Activities - 2HRS"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 8054,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Abseiling",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 421,
            "number_instructors": 2,
            "number_participants": 10,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4961,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": [
                1546
            ],
            "name": "ACTIVITY - Air Rifle Shooting",
            "description": null,
            "terms_and_conditions": "When booking rifle shooting, it is a legal requirement to complete a Section 21 form before undertaking the activity, this can be found on our website. If you do not have a completed form, you will not be able to participate in the activity. Please note this is legislation and, therefore, must be adhered to by all parties.",
            "instructions": "Please ensure Section 21 form is completed prior to activities. If no form is present the individual is legally not allowed to participate.",
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": true,
            "campsite_instructor_type_id": 422,
            "number_instructors": 1,
            "number_participants": 5,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 5403,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": [
                4967,
                4964
            ],
            "name": "ACTIVITY - Angel Throwing",
            "description": null,
            "terms_and_conditions": "Please note this activity is only suitable for ages 8 and above.",
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 15,
            "hide_externally": false,
            "staff_only": true,
            "campsite_instructor_type_id": 420,
            "number_instructors": 1,
            "number_participants": 5,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4962,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Archery",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 12,
            "hide_externally": false,
            "staff_only": true,
            "campsite_instructor_type_id": 419,
            "number_instructors": 1,
            "number_participants": 6,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 7693,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Climbing",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 421,
            "number_instructors": 2,
            "number_participants": 10,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4967,
            "parent_id": 0,
            "campsite_session_id": 2103,
            "campsite_price_type_id": 2488,
            "availability_rules": null,
            "also_check": [
                4964,
                5403
            ],
            "name": "ACTIVITY - Tomahawk Throwing",
            "description": null,
            "terms_and_conditions": "Please note this activity is only suitable for ages 10 and above.",
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 15,
            "hide_externally": false,
            "staff_only": true,
            "campsite_instructor_type_id": 420,
            "number_instructors": 1,
            "number_participants": 10,
            "minimum_instructors": 2,
            "available": true,
            "session": {
                "id": 2103,
                "name": "XX Activity - Instructor Led CJ"
            },
            "price": {
                "id": 2488,
                "name": "\u00a340 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4971,
            "parent_id": 0,
            "campsite_session_id": 2105,
            "campsite_price_type_id": 2485,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Pioneering",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 40,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2105,
                "name": "XX Activity - Pioneering CJ"
            },
            "price": {
                "id": 2485,
                "name": "\u00a310 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4970,
            "parent_id": 0,
            "campsite_session_id": 2102,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Rafting",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2102,
                "name": "XX Activity - Rafting CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4963,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": [
                5401
            ],
            "name": "ACTIVITY - 3D Maze",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4965,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2491,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Adventure Run",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 12,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 1,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2491,
                "name": "\u00a30 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4944,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Bouldering Hut",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4975,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2486,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Human Table Football",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2486,
                "name": "\u00a315 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4969,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Kayaking",
            "description": null,
            "terms_and_conditions": "Please note to undertake this activity you must provide your own instructor.",
            "instructions": "An qualified instructor must be provided for this activity. Thorrington is unable to provide such an instructor.",
            "quantity": 1,
            "min_people": 0,
            "max_people": 12,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4966,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2486,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Mini Crossbows",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2486,
                "name": "\u00a315 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4977,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2491,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Orienteering Trail",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2491,
                "name": "\u00a30 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4968,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Pedal Karts",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4976,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2491,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Photo Quiz",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2491,
                "name": "\u00a30 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4964,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2486,
            "availability_rules": null,
            "also_check": [
                4967
            ],
            "name": "ACTIVITY - Slacklining",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2486,
                "name": "\u00a315 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 4973,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 2487,
            "availability_rules": null,
            "also_check": null,
            "name": "ACTIVITY - Slingshots",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 1,
            "min_people": 0,
            "max_people": 20,
            "hide_externally": false,
            "staff_only": false,
            "campsite_instructor_type_id": 0,
            "number_instructors": 0,
            "number_participants": 0,
            "minimum_instructors": 0,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 2487,
                "name": "\u00a325 Activities"
            },
            "bookings": [],
            "pictures": []
        },
        {
            "id": 8722,
            "parent_id": 0,
            "campsite_session_id": 2104,
            "campsite_price_type_id": 4042,
            "availability_rules": null,
            "also_check": null,
            "name": "Midweek Opening",
            "description": null,
            "terms_and_conditions": null,
            "instructions": null,
            "quantity": 10,
            "min_people": 0,
            "max_people": 0,
            "hide_externally": true,
            "staff_only": true,
            "campsite_instructor_type_id": 461,
            "number_instructors": 1,
            "number_participants": 100,
            "minimum_instructors": 1,
            "available": true,
            "session": {
                "id": 2104,
                "name": "XX Activity - Self Led CJ"
            },
            "price": {
                "id": 4042,
                "name": "\u00a30 Add On"
            },
            "bookings": [],
            "pictures": []
        }
    ],
    "meta": {
        "venue_name": "Thorrington Scout Camp",
        "has_credit": true,
        "has_contact_details": true
    }
}
```

---

### GET /api/bookings/{id}/comments (or similar)
**Purpose:** Get comments for a booking

**URL:** `https://www.onlinescoutmanager.co.uk/v3/comments/campsite_booking/153277/list?section_id=56710`

**Response Format:**
```json
{
    "status": true,
    "error": null,
    "data": [
        {
            "id": 1137166,
            "section_id": 56710,
            "user_id": 481068,
            "associated_type": "campsite_booking",
            "associated_id": "153277",
            "comment": "Rebooked due to auto cancel for no deposit.\nBooker stated deposit is incoming.",
            "number_likes": 0,
            "created_at": "2025-11-09 22:12:12",
            "updated_at": "2025-11-09 22:12:12",
            "local_time": "09\/11\/2025 22:12",
            "engagements": [],
            "user": {
                "photo_url": false,
                "full_name": "Chris Jay"
            }
        },
        {
            "id": 1138177,
            "section_id": 56710,
            "user_id": 574496,
            "associated_type": "campsite_booking",
            "associated_id": "153277",
            "comment": "Deposit received today, payment added to booking.",
            "number_likes": 0,
            "created_at": "2025-11-11 18:47:48",
            "updated_at": "2025-11-11 18:47:48",
            "local_time": "11\/11\/2025 18:47",
            "engagements": [],
            "user": {
                "photo_url": false,
                "full_name": "Tammy  Lorimer"
            }
        }
    ],
    "meta": {
        "can_edit_self": true,
        "can_edit_others": false,
        "can_delete_self": true,
        "can_delete_others": false,
        "can_add": true,
        "notification_topics": {
            "comments\/campsite_booking\/153277": {
                "label": "Comments on this thread",
                "section_id": 56710,
                "permission_type": "campsite_bookings",
                "permission_value": "view",
                "javascript_data": []
            },
            "comments\/campsite_bookings_for_section\/56710": {
                "label": "Comments on any venue booking",
                "section_id": 56710,
                "permission_type": "campsite_bookings",
                "permission_value": "view"
            }
        },
        "has_uploads": false
    }
}
```

---

### POST /api/bookings/{id}/comments (or similar)
**Purpose:** Add comment to booking (for Phase 2)

**URL:** `https://www.onlinescoutmanager.co.uk/v3/comments/campsite_booking/153277/add/?section_id=56710`

**Request Body:**
Form data
```
comment=Test+comment
```

---

---


---

## Rate Limiting

**Observed Limits:** [TBD]
- Requests per minute: [TBD]
- Requests per hour: [TBD]
- Any retry-after headers: [TBD]

**Recommendations:**
- [Add throttling strategy if needed]

---

## Pagination

**Method:** [Query params / Link headers / Cursor-based]

**Parameters:**
- `page`: [TBD]
- `limit`: [TBD]
- `offset`: [TBD]

**Example:**
```
[Document pagination example]
```

---

## Error Responses

**Common Error Codes:**
- 401: [Unauthorized - what triggers this?]
- 403: [Forbidden - what triggers this?]
- 404: [Not found]
- 429: [Rate limit exceeded]
- 500: [Server error]

**Error Response Format:**
```json
{
  "example": "paste actual error response"
}
```

---

## Notes and Observations

### Security Considerations
- [Note any CSRF tokens, session management, etc.]

### Data Quirks
- [Note any unusual data formats, edge cases, or inconsistencies]

### API Stability
- [Note any versioning, deprecation warnings, etc.]

### What Defines "Provisional"?
- [Document what makes a booking "provisional" vs "confirmed"]
- [Is there a specific status field? What are all possible values?]

### What Makes a Comment "New"?
- [Is there a flag? Or is it based on timestamp comparison?]
- [How do we track which comments we've already seen?]

---

## Next Steps

After completing this documentation:

1. Review findings with team
2. Implement OsmService in Task 11 based on these discoveries
3. Test with real OSM data
4. Update documentation with any corrections

---

**Last Updated:** [Date]
**Updated By:** [Your Name]
**OSM Version:** [Note the OSM version if visible]
