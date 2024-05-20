/*
 *  Document   : op_auth_signin.js
 *  Author     : pixelcave
 *  Description: Custom JS code used in Sign In Page
 */

// Form Validation, for more examples you can check out https://github.com/jzaefferer/jquery-validation
class pageAuthSignIn {
    /*
     * Init Sign In Form Validation
     *
     */
    static initValidationSignIn() {
        // Load default options for jQuery Validation plugin
        Codebase.helpers('jq-validation');

        // Init Form Validation
        jQuery('.js-validation-signin').validate({
            rules: {
                'login-username': {
                    required: true
                },
                'login-password': {
                    required: true
                }
            },
            messages: {
                'login-username': {
                    required: 'Please enter your employee no.'
                },
                'login-password': {
                    required: 'Please provide a password'
                }
            }
        });
    }

    /*
     * Init functionality
     *
     */
    static init() {
        this.initValidationSignIn();
    }
}

// Initialize when page loads
Codebase.onLoad(pageAuthSignIn.init());
